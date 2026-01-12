using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Shukachi.SeedAgent.Api.Plugins
{
    public sealed class ActPlugin
    {
        [KernelFunction("act_on_task")]
        [Description("Receives a task description . Parameters: task (str) - what to act on. Returns: str - a confirmation message.")]
        public Task<string> ActAsync(
            [Description("The task description to act on.")] string task,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult($"Acted on task: {task}");
        }
    }
}
