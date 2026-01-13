using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Shukachi.SeedAgent.Api.Services;

namespace Shukachi.SeedAgent.Api.Plugins
{
    public sealed class KnowledgeStorePlugin
    {
        private readonly QdrantClient _qdrantClient;
        private readonly ILogger<KnowledgeStorePlugin> _logger;

        public KnowledgeStorePlugin(QdrantClient qdrantClient, ILogger<KnowledgeStorePlugin> logger)
        {
            _qdrantClient = qdrantClient;
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
            return StoreAndConfirmAsync(text, uid, cancellationToken);
        }

        private async Task<string> StoreAndConfirmAsync(string text, string uid, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Storing message in Qdrant for uid {Uid}", uid);
            await _qdrantClient.StoreMessageAsync(text, uid, cancellationToken);
            _logger.LogInformation("Stored message in Qdrant for uid {Uid}", uid);
            return $"Stored for {uid}: {text}";
        }
    }
}
