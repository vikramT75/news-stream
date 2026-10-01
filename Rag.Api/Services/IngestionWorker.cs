#pragma warning disable CS0618
using System.Text.Json;
using Confluent.Kafka;
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

        public IngestionWorker(ILogger<IngestionWorker> logger, IConfiguration configuration, Kernel kernel)
        {
            _logger = logger;
            _configuration = configuration;
            _kernel = kernel;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Kafka Ingestion Worker started. Listening to 'global-news' topic...");

            _ = Task.Run(async () =>
            {
                var embeddingGenerator = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                var consumerConfig = new ConsumerConfig
                {
                    BootstrapServers = "localhost:9092",
                    GroupId = "rag-api-consumer-group",
                    AutoOffsetReset = AutoOffsetReset.Earliest
                };

                using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
                consumer.Subscribe("global-news");

                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = consumer.Consume(stoppingToken);
                            if (consumeResult == null) continue;

                            var messageJson = consumeResult.Message.Value;
                            using var doc = JsonDocument.Parse(messageJson);
                            var content = doc.RootElement.GetProperty("Content").GetString() ?? "";
                            var source = doc.RootElement.GetProperty("Source").GetString() ?? "Unknown";
                            var ingestionTime = DateTime.UtcNow;

                            _logger.LogInformation($"Consumed from Kafka: {content}");

                            var embedding = await embeddingGenerator.GenerateEmbeddingAsync(content, cancellationToken: stoppingToken);
                            var embeddingJson = JsonSerializer.Serialize(embedding.ToArray());

                            using var connection = new SqlConnection(connectionString);
                            var query = @"
                                INSERT INTO DocumentChunks (Content, Embedding, Source, IngestionTime) 
                                VALUES (@Content, @Embedding, @Source, @IngestionTime)";

                            await connection.ExecuteAsync(query, new
                            {
                                Content = content,
                                Embedding = embeddingJson,
                                Source = source,
                                IngestionTime = ingestionTime
                            });
                            
                            _logger.LogInformation("Successfully embedded and saved to database.");
                        }
                        catch (ConsumeException e)
                        {
                            _logger.LogError($"Consume error: {e.Error.Reason}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    consumer.Close();
                }
            }, stoppingToken);

            return Task.CompletedTask;
        }
    }
}
