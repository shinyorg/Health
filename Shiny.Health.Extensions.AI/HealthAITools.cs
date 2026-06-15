using Microsoft.Extensions.AI;

namespace Shiny.Health.Extensions.AI;

/// <summary>
/// Bundle of <see cref="AITool"/> instances generated for the health areas you opt-in to via
/// <c>AddHealthAITools</c>. Resolve this from DI and pass <see cref="Tools"/> to your
/// <c>IChatClient</c> call (e.g. <c>ChatOptions.Tools</c>).
/// </summary>
public sealed class HealthAITools
{
    /// <summary>The generated tools. Areas/operations not opted-in are invisible to the LLM.</summary>
    public IReadOnlyList<AITool> Tools { get; }

    internal HealthAITools(IReadOnlyList<AITool> tools) => this.Tools = tools;
}
