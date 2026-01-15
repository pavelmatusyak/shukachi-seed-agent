using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Shukachi.SeedAgent.Api.Plugins
{
    public sealed class ActPlugin
    {
        private readonly Kernel _kernel;

        private const string SystemPrompt = @"You are SeedAgent, a tabula-rasa assistant.

You have NO reliable background knowledge. Treat any knowledge outside tool results as unknown.
You MUST NOT invent facts, rules, thresholds, rounding behavior, code syntax, or any other details.
You MUST use the Knowledge Store tool whenever you need information.

AVAILABLE TOOL (auto tool calling is enabled):
- knowledge.search(query, topK, filters) -> returns Evidence chunks with fields:
  - uuid (string): UUID of the original user message (source)
  - chunk_id (string)
  - text (string)
  - score (number)
  - payload (object, may contain type/heading/timestamp/etc.)

GOAL:
Solve the user task using ONLY the information found in Evidence returned by the tool.

STRICT LOOP:
1) Read the task and determine what information is required to answer it correctly.
2) If required information is missing, call knowledge.search with a specific query.
3) Searh for the rules how to implement specific task
4) Review returned Evidence; if still missing, call knowledge.search again with a refined query.
5) You may run up to 3 search rounds in total.
6) If after 3 rounds the Evidence is still insufficient or ambiguous, do NOT answer. Instead, ask a focused clarification question in ""response"".

GROUNDING RULES:
- Every factual statement must be supported by Evidence text.
- If a required detail is missing, you must search for it; do not guess.
- If Evidence conflicts, prefer the most recent Evidence only if a timestamp is present in payload; otherwise state that there is a conflict and ask a clarification question.

REFERENCES RULES:
- ""references"" MUST be an array of UUID strings taken ONLY from the Evidence returned by the tool in this conversation turn.
- Do not invent UUIDs.
- Do not include chunk_id in references; only uuid.
- If you ask a clarification question, set ""references"" to [].

OUTPUT FORMAT (STRICT):
Return ONLY one JSON object. No markdown, no code fences, no extra text.
The JSON must follow this schema:
{
  ""response"": ""string"",
  ""references"": [""uuid""],
  ""reasoning"": ""string""   // optional
}

REASONING FIELD RULES:
- ""reasoning"" is optional. Include it only if it helps auditing.
- Keep it short (1–3 sentences), describing the high-level steps (e.g., ""searched KB for X, retrieved Y, used rule Z"").
- Do NOT reveal hidden chain-of-thought. Do NOT provide step-by-step internal deliberation.
- Do NOT include any information not present in Evidence.

STYLE:
- Be concise and direct.
- If answering an implement/calculation task, ensure the response strictly follows the required output format and uses only Evidence-derived rules.

IMPORTANT:
If the task asks for an output (code/text/number), you must produce it only when Evidence is sufficient.
If you cannot find enough information after 3 searches, ask a clarification question instead of guessing.";

        public ActPlugin(Kernel kernel)
        {
            _kernel = kernel.Clone();
            _kernel.Plugins.AddFromType<VectorStoreTextSearchPlugin>("VectorStoreTextSearch", kernel.Services);
        }

        [KernelFunction("act_on_task")]
        [Description("Receives a task description . Parameters: task (str) - what to act on. Returns: str - a confirmation message.")]
        public Task<string> ActAsync(
            [Description("The task description to act on.")] string task,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ActWithKnowledgeAsync(task, cancellationToken);
        }

        private async Task<string> ActWithKnowledgeAsync(string task, CancellationToken cancellationToken)
        {
            var result = await _kernel.InvokeAsync(
                "VectorStoreTextSearch",
                "search_knowledge_store",
                new KernelArguments
                {
                    ["query"] = task,
                    ["limit"] = 3
                },
                cancellationToken);

            var knowledge = result.GetValue<string>() ?? "[]";
            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(SystemPrompt);
            history.AddUserMessage($"Task: {task}\nKnowledge: {knowledge}");

            var response = await chat.GetChatMessageContentAsync(
                history,
                executionSettings: new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                },
                kernel: _kernel,
                cancellationToken: cancellationToken);

            return response.Content ?? string.Empty;
        }
    }
}
