#pragma warning disable CS0618
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace Rag.Api.Services
{
    public class IngestionWorker : BackgroundService
    {
        private readonly ILogger<IngestionWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly Kernel _kernel;

        // Mock data feed arrays
        private readonly string[] _companies = { "Microsoft", "Apple", "Nvidia", "Tesla", "Amazon" };
        private readonly string[] _events = { 
            "beats earnings expectations by 15%.", 
            "announces revolutionary new AI product.", 
            "faces severe supply chain delays in Asia.", 
            "CEO unexpectedly steps down.", 
            "acquires promising startup for $2B." 
        };

        public IngestionWorker(ILogger<IngestionWorker> logger, IConfiguration configuration, Kernel kernel)
        {
            _logger = logger;
            _configuration = configuration;
            _kernel = kernel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Ingestion Worker started. Simulating live financial feed...");


            var embeddingGenerator = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var rand = new Random();

            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Generate Fake Live News
                var company = _companies[rand.Next(_companies.Length)];
                var eventStr = _events[rand.Next(_events.Length)];
                var content = $"[{DateTime.UtcNow:HH:mm:ss}] BREAKING: {company} {eventStr}";
                
                _logger.LogInformation($"Ingesting: {content}");

                try
                {
                    // 2. Generate Vector Embedding via Semantic Kernel
                    var embedding = await embeddingGenerator.GenerateEmbeddingAsync(content, cancellationToken: stoppingToken);
                    
                    // Convert float[] to JSON array string for our SQL Server storage
                    var embeddingJson = JsonSerializer.Serialize(embedding.ToArray());

                    // 3. Save to SQL Server via Dapper
                    using var connection = new SqlConnection(connectionString);
                    var query = @"
                        INSERT INTO DocumentChunks (Content, Embedding, Source, IngestionTime) 
                        VALUES (@Content, @Embedding, @Source, @IngestionTime)";

                    await connection.ExecuteAsync(query, new
                    {
                        Content = content,
                        Embedding = embeddingJson,
                        Source = "Simulated Bloomberg Feed",
                        IngestionTime = DateTime.UtcNow
                    });
                    
                    _logger.LogInformation("Successfully saved vector to database.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to ingest chunk: {ex.Message}");
                }

                // Wait 20 seconds before generating the next news item
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
        }
    }
}
