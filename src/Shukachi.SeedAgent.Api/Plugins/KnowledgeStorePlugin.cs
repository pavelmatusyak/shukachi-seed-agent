using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Shukachi.SeedAgent.Api.Services;
using Refit;

namespace Shukachi.SeedAgent.Api.Plugins
{
    public sealed class KnowledgeStorePlugin
    {
        private readonly QdrantClient _qdrantClient;
        private readonly IEmbeddingServerClient _embeddingServerClient;
        private readonly ILogger<KnowledgeStorePlugin> _logger;

        public KnowledgeStorePlugin(
            QdrantClient qdrantClient,
            IEmbeddingServerClient embeddingServerClient,
            ILogger<KnowledgeStorePlugin> logger)
        {
            _qdrantClient = qdrantClient;
            _embeddingServerClient = embeddingServerClient;
            _logger = logger;
        }

        [KernelFunction("add_to_knowledge_store")]
        [Description(@"Adds text to the Qdrant knowledge store and returns a confirmation string. 
        Parameters: text (str) - the content to store; uid (str) - the user identifier.
        Returns: str - a confirmation message containing the stored text.")]
        public Task<string> AddAsync(
            [Description("The content to store in the knowledge store.")] string text,
            [Description("The user identifier to associate with the stored text.")] string uid,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation(
                "Add knowledge store item with uid {Uid} and text {Text}",
                uid,
                text);
            return StoreAndConfirmAsync(text, uid, cancellationToken);
        }

        private async Task<string> StoreAndConfirmAsync(string text, string uid, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Storing message in Qdrant for uid {Uid}", uid);
            var vector = await GetEmbeddingAsync(text, cancellationToken);
            await _qdrantClient.StoreMessageAsync(text, uid, vector, cancellationToken);
            _logger.LogInformation("Stored message in Qdrant for uid {Uid}", uid);
            return $"Stored for {uid}: {text}";
        }

        private async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            try
            {
                var payload = await _embeddingServerClient.EmbedDocumentAsync(
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
    }
}
