using Microsoft.SemanticKernel;
using Shukachi.SeedAgent.Api.Plugins;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<KnowledgeStorePlugin>();

var modelId = builder.Configuration["LLM_MODEL_ID"];
var apiKey = builder.Configuration["LLM_MODEL_KEY"];
if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("LLM_MODEL_ID and LLM_MODEL_KEY must be set.");
}

builder.Services.AddKernel()
    .AddOpenAIChatCompletion(modelId, apiKey);

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
