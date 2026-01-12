using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Shukachi.SeedAgent.Api.Plugins
{
    public sealed class KnowledgeStorePlugin
    {
        [KernelFunction("add_to_knowledge_store")]
        [Description("Adds text to a dummy knowledge store and returns a confirmation string. Parameters: text (str) - the content to store. Returns: str - a confirmation message containing the stored text.")]
        public Task<string> AddAsync(
            [Description("The content to store in the dummy knowledge store.")] string text,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult($"Stored: {text}");
        }
    }
}
