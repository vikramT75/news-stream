using Microsoft.SemanticKernel;
using Rag.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var ollamaEndpoint = new Uri(builder.Configuration["Ollama:Endpoint"] ?? "http://localhost:11434/v1/");
var chatModelId = builder.Configuration["Ollama:ChatModelId"] ?? "llama3.2:3b";
var embeddingModelId = builder.Configuration["Ollama:EmbeddingModelId"] ?? "nomic-embed-text";

var ollamaClient = new HttpClient { BaseAddress = ollamaEndpoint };

builder.Services.AddKernel()
    .AddOpenAIChatCompletion(chatModelId, "dummy-key", httpClient: ollamaClient)
    .AddOpenAITextEmbeddingGeneration(embeddingModelId, "dummy-key", httpClient: ollamaClient);

builder.Services.AddHostedService<IngestionWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
