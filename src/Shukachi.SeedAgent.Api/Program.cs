using Microsoft.SemanticKernel;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Refit;
using Serilog;
using Serilog.Events;
using Shukachi.SeedAgent.Api.Plugins;
using Shukachi.SeedAgent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var seqServerUrl = builder.Configuration["SEQ_SERVER_URL"] ?? "http://seq:5341";
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Seq(seqServerUrl);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<KnowledgeStorePlugin>();
builder.Services.AddSingleton<ActPlugin>();
builder.Services.AddSingleton<VectorStoreTextSearchPlugin>();
builder.Services.AddRefitClient<IEmbeddingServerClient>()
    .ConfigureHttpClient(client =>
    {
        var baseUrl = builder.Configuration["EMBEDDING_SERVER_URL"]
            ?? "http://shukachi.seedagent.embeddingserver:3001";
        client.BaseAddress = new Uri(baseUrl);
    });
builder.Services.Configure<QdrantOptions>(options =>
{
    options.GrpcHost = builder.Configuration["QDRANT_GRPC_HOST"] ?? options.GrpcHost;
    if (int.TryParse(builder.Configuration["QDRANT_GRPC_PORT"], out var grpcPort))
    {
        options.GrpcPort = grpcPort;
    }
    options.Collection = builder.Configuration["QDRANT_COLLECTION"] ?? options.Collection;
    if (int.TryParse(builder.Configuration["QDRANT_VECTOR_SIZE"], out var vectorSize))
    {
        options.VectorSize = vectorSize;
    }
});
builder.Services.AddSingleton<QdrantClient>();

var modelId = builder.Configuration["LLM_MODEL_ID"];
var apiKey = builder.Configuration["LLM_MODEL_KEY"];
if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("LLM_MODEL_ID and LLM_MODEL_KEY must be set.");
}

builder.Services.AddKernel()
    .AddOpenAIChatCompletion(modelId, apiKey);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource.AddService(builder.Environment.ApplicationName))
    .WithTracing(tracing =>
        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Microsoft.SemanticKernel")
            .AddOtlpExporter())
    .WithMetrics(metrics =>
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddMeter("Microsoft.SemanticKernel")
            .AddOtlpExporter());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
