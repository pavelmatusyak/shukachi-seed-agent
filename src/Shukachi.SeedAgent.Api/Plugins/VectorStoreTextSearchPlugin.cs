using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Qdrant.Client.Grpc;
using Refit;
using Shukachi.SeedAgent.Api.Services;

namespace Shukachi.SeedAgent.Api.Plugins
{
    public sealed class VectorStoreTextSearchPlugin
    {
        private readonly QdrantClient _qdrantClient;
        private readonly IEmbeddingServerClient _embeddingServerClient;
        private readonly ILogger<VectorStoreTextSearchPlugin> _logger;

        public VectorStoreTextSearchPlugin(
            QdrantClient qdrantClient,
            IEmbeddingServerClient embeddingServerClient,
            ILogger<VectorStoreTextSearchPlugin> logger)
        {
            _qdrantClient = qdrantClient;
            _embeddingServerClient = embeddingServerClient;
            _logger = logger;
        }

        [KernelFunction("search_knowledge_store")]
        [Description(@"Searches the Qdrant knowledge store with a vector query and returns the top results as JSON.
        Parameters: query (str) - the text to search for; uid (str, optional) - filter by user identifier; limit (int, optional) - max results.
        Returns: str - JSON array of search hits (score, message, uid, created_at_utc).")]
        public async Task<string> SearchAsync(
            [Description("Text to search for.")] string query,
            [Description("Maximum number of results to return.")] int limit = 5,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "[]";
            }

            var vector = await GetEmbeddingAsync(query, cancellationToken);
            var result = await _qdrantClient.SearchMessagesAsync(vector, null, limit, cancellationToken);

            var hits = result.Result.Select(point => new SearchHit
            {
                Score = point.Score,
                Message = GetPayloadString(point, "message"),
                Uid = GetPayloadString(point, "uid"),
                CreatedAtUtc = GetPayloadString(point, "created_at_utc")
            });

            return JsonSerializer.Serialize(hits);
        }

        private async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            try
            {
                var payload = await _embeddingServerClient.EmbedSearchAsync(
                    new EmbedRequest { Text = text },
                    cancellationToken);

                if (payload?.Vector == null || payload.Vector.Length == 0)
                {
                    throw new InvalidOperationException("Embedding server returned an empty vector.");
                }

                return payload.Vector;
            }
            catch (ApiException ex)
            {
                _logger.LogError(
                    ex,
                    "Embedding server returned {StatusCode}: {Content}",
                    (int)ex.StatusCode,
                    ex.Content ?? string.Empty);
                throw new InvalidOperationException("Embedding server request failed.", ex);
            }
        }

        private static string? GetPayloadString(ScoredPoint point, string key)
        {
            return point.Payload.TryGetValue(key, out var value)
                ? value.StringValue
                : null;
        }

        private sealed class SearchHit
        {
            public float Score { get; set; }
            public string? Message { get; set; }
            public string? Uid { get; set; }
            public string? CreatedAtUtc { get; set; }
        }
    }
}
