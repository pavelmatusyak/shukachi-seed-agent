using System.Text.Json.Serialization;
using Tokenizers.HuggingFace.Tokenizer;
using OnnxSessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Paths (relative to app base directory)
var baseDir = AppContext.BaseDirectory;
var modelPath = Path.Combine(baseDir, "models", "e5_onnx", "model.onnx");
var tokenizerPath = Path.Combine(baseDir, "models", "e5_onnx", "tokenizer.json");

// Tunables
const int MaxLength = 512; // try 256 if you want faster
const bool UseCpuOnly = true;

// Load tokenizer + ONNX
var tokenizer = Tokenizer.FromFile(tokenizerPath);

var sessOpts = new OnnxSessionOptions();
if (UseCpuOnly)
{
    // default CPU is fine
}
var session = new InferenceSession(modelPath, sessOpts);

// Determine input/output names robustly
string inputIdsName = "input_ids";
string attentionMaskName = "attention_mask";
string? tokenTypeIdsName = session.InputMetadata.ContainsKey("token_type_ids") ? "token_type_ids" : null;

// Prefer "last_hidden_state" if present; else first output
string outputName = session.OutputMetadata.ContainsKey("last_hidden_state")
    ? "last_hidden_state"
    : session.OutputMetadata.Keys.First();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        ok = true,
        model = Path.GetFileName(modelPath),
        output = outputName,
        inputs = session.InputMetadata.Keys.ToArray(),
        dim = session.OutputMetadata[outputName].Dimensions.LastOrDefault()
    });
});

IResult EmbedText(string? text, string mode)
{
    if (string.IsNullOrWhiteSpace(text))
        return Results.BadRequest(new { error = "text is required" });

    var normalizedMode = mode.Trim().ToLowerInvariant();
    var prefix = normalizedMode == "query" ? "query: " : "passage: ";
    var fullText = prefix + text;

    // 1) Tokenize
    var enc = tokenizer.Encode(
        fullText,
        addSpecialTokens: true,
        input2: null,
        includeTypeIds: false,
        includeTokens: false,
        includeWords: false,
        includeOffsets: false,
        includeSpecialTokensMask: false,
        includeAttentionMask: true,
        includeOverflowing: false).First();

    // ids + attention mask (int -> long for ORT)
    var ids = enc.Ids.Select(x => (long)x).ToList();
    var att = enc.AttentionMask.Select(x => (long)x).ToList();

    // Truncate / pad to MaxLength
    if (ids.Count > MaxLength)
    {
        ids = ids.Take(MaxLength).ToList();
        att = att.Take(MaxLength).ToList();
    }
    else if (ids.Count < MaxLength)
    {
        int pad = MaxLength - ids.Count;
        ids.AddRange(Enumerable.Repeat(0L, pad));
        att.AddRange(Enumerable.Repeat(0L, pad));
    }

    var inputIds = new DenseTensor<long>(new[] { 1, MaxLength });
    var attentionMask = new DenseTensor<long>(new[] { 1, MaxLength });

    for (int i = 0; i < MaxLength; i++)
    {
        inputIds[0, i] = ids[i];
        attentionMask[0, i] = att[i];
    }

    // Optional token_type_ids (many models don't need it; if they do, fill zeros)
    DenseTensor<long>? tokenTypeIds = null;
    if (tokenTypeIdsName != null)
    {
        tokenTypeIds = new DenseTensor<long>(new[] { 1, MaxLength });
        // all zeros is typical for single-sequence encodings
        // (leave as default zeros)
    }

    // 2) Run ONNX
    var inputs = new List<NamedOnnxValue>
    {
        NamedOnnxValue.CreateFromTensor(inputIdsName, inputIds),
        NamedOnnxValue.CreateFromTensor(attentionMaskName, attentionMask),
    };
    if (tokenTypeIdsName != null && tokenTypeIds != null)
        inputs.Add(NamedOnnxValue.CreateFromTensor(tokenTypeIdsName, tokenTypeIds));

    using var results = session.Run(inputs);

    var outputTensor = results.First(r => r.Name == outputName).AsTensor<float>(); // [1, seq, hidden]
    int seqLen = outputTensor.Dimensions[1];
    int hidden = outputTensor.Dimensions[2];

    // 3) Mean pooling over tokens where attention_mask=1
    var pooled = new float[hidden];
    float denom = 0f;

    for (int t = 0; t < seqLen; t++)
    {
        if (attentionMask[0, t] == 0) continue;
        denom += 1f;

        for (int h = 0; h < hidden; h++)
            pooled[h] += outputTensor[0, t, h];
    }

    if (denom > 0f)
    {
        for (int h = 0; h < hidden; h++)
            pooled[h] /= denom;
    }

    // 4) L2 normalize
    L2NormalizeInPlace(pooled);

    // Return
    return Results.Ok(new EmbedResponse
    {
        Dim = pooled.Length,
        Vector = pooled
    });
}

app.MapPost("/embed", (EmbedRequest req) =>
{
    var mode = req.Mode ?? "passage";
    return EmbedText(req.Text, mode);
});

app.MapPost("/embed-doc", (EmbedRequest req) => EmbedText(req.Text, "passage"));
app.MapPost("/embed-search", (EmbedRequest req) => EmbedText(req.Text, "query"));

app.Lifetime.ApplicationStopping.Register(() =>
{
    session.Dispose();
});

app.Run();

static void L2NormalizeInPlace(float[] v)
{
    double sum = 0;
    for (int i = 0; i < v.Length; i++)
        sum += (double)v[i] * v[i];

    var norm = Math.Sqrt(sum);
    if (norm < 1e-12) return;

    var inv = (float)(1.0 / norm);
    for (int i = 0; i < v.Length; i++)
        v[i] *= inv;
}

public sealed class EmbedRequest
{
    public string? Text { get; set; }
    public string? Mode { get; set; } // "query" | "passage"
}

public sealed class EmbedResponse
{
    public int Dim { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
}


