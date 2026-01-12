using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Shukachi.SeedAgent.Api.Models;
using Shukachi.SeedAgent.Api.Plugins;

namespace Shukachi.SeedAgent.Api.Controllers
{
    [ApiController]
    [Route("")]
    public sealed class TextController : ControllerBase
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletion;
        private readonly OpenAIPromptExecutionSettings _openAIPromptExecutionSettings;


        public TextController(Kernel kernel)
        {
            _kernel = kernel.Clone();
            _kernel.Plugins.AddFromType<KnowledgeStorePlugin>("KnowlegeStore");
            _chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

            _openAIPromptExecutionSettings = new() 
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

        }

        [HttpPost("text")]
        public async Task<ActionResult<TextResponse>> PostText([FromBody] TextRequest request, CancellationToken cancellationToken)
        {
            const string systemPrompt = "You are a helpful assistant that answers briefly.";

            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt);
            history.AddUserMessage(request.Text);

            var result = await _chatCompletion.GetChatMessageContentAsync(
                history,
                executionSettings: _openAIPromptExecutionSettings,
                kernel: _kernel,
                cancellationToken: cancellationToken);

            var response = new TextResponse
            {
                Response = result.Content ?? string.Empty,
                References = [],
                Reasoning = null
            };

            return Ok(response);
        }
    }
}
