#pragma warning disable CS0618
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using System.Text.Json;

namespace Rag.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly Kernel _kernel;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatController> _logger;

        public ChatController(Kernel kernel, IConfiguration configuration, ILogger<ChatController> logger)
        {
            _kernel = kernel;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("stream")]
        public async Task StreamChat([FromBody] ChatRequest request)
        {
            // Set headers for Server-Sent Events (SSE)
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            if (string.IsNullOrWhiteSpace(request.Prompt)) return;

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var embeddingGenerator = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

            try
            {
                // 1. Embed the User's Query using Ollama
                var queryEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(request.Prompt);
                var queryEmbeddingJson = JsonSerializer.Serialize(queryEmbedding.ToArray());

                // 2. Perform Time-Decay Vector Search in SQL Server
                using var connection = new SqlConnection(connectionString);
                var sql = "EXEC sp_SearchWithTemporalDecay @QueryVector = @QueryVector, @TopK = 3";
                var contextChunks = await connection.QueryAsync<dynamic>(sql, new { QueryVector = queryEmbeddingJson });

                var contextText = string.Join("\n\n", contextChunks.Select(c => $"[Time: {c.IngestionTime:HH:mm:ss} | Source: {c.Source}]\n{c.Content}"));

                // 3. Construct the Prompt
                var systemPrompt = $@"You are a real-time financial assistant. 
Answer the user's question using ONLY the latest information provided below.
Because we apply a temporal decay algorithm, the context below is guaranteed to be the freshest and most relevant.
If the context does not contain the answer, say 'I don't have enough recent information to answer that.'

<Context>
{contextText}
</Context>";

                var chatHistory = new ChatHistory(systemPrompt);
                chatHistory.AddUserMessage(request.Prompt);

                // 4. Stream the Response to the UI
                await foreach (var chunk in chatCompletion.GetStreamingChatMessageContentsAsync(chatHistory))
                {
                    if (chunk.Content != null)
                    {
                        // Clean up newlines so it doesn't break the SSE protocol
                        var safeContent = chunk.Content.Replace("\n", "<br>");
                        await Response.WriteAsync($"data: {safeContent}\n\n");
                        await Response.Body.FlushAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Chat error: {ex.Message}");
                await Response.WriteAsync($"data: Error: {ex.Message}\n\n");
                await Response.Body.FlushAsync();
            }

            await Response.WriteAsync("data: [DONE]\n\n");
            await Response.Body.FlushAsync();
        }
    }

    public class ChatRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }
}
