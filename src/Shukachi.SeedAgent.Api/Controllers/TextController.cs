using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;
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
        private readonly KnowledgeStorePlugin _knowledgeStorePlugin;

        public TextController(Kernel kernel, KnowledgeStorePlugin knowledgeStorePlugin)
        {
            _kernel = kernel.Clone();
            _kernel.Plugins.AddFromType<KnowledgeStorePlugin>("KnowlegeStore", kernel.Services);
            _kernel.Plugins.AddFromType<ActPlugin>("Act", kernel.Services);
            _chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
            _knowledgeStorePlugin = knowledgeStorePlugin;

            _openAIPromptExecutionSettings = new() 
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

        }

        [HttpPost("text")]
        public async Task<ActionResult<TextResponse>> PostText([FromBody] TextRequest request, CancellationToken cancellationToken)
        {
            var history = new ChatHistory();
            history.AddSystemMessage(GetSystemPromtp());
            history.AddUserMessage(JsonSerializer.Serialize(new
            {
                uid = request.Uid,
                text = request.Text
            }));

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

        private string GetSystemPromtp()
        {
            return @"
Determine the user's intent from their message in Ukrainian. 
The intent may be either providing information
(e.g., infomation, facts data or code examples such as ""ціна квартири 100 гривень"") or 
requesting an action (e.g., commands such as ""сгенерувати документ""). 
You cannot ask clarifying questions. 
After identifying the intent, select and call the most appropriate tool for fulfilling the intent.
Store and utilize all relevant knowledge you acquire to improve over time

Consider every message carefully:  
- First, analyze the user's message step-by-step to reason about their intent.  
- Then, explicitly state the recognized intent and classification (information or action).  
- Never start with the conclusion; always reason first, then present your conclusion.  
- Never ask clarifying questions.  
- If an action is required, specify which tool should be called and why..";
        }

    }
}
