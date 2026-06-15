namespace Shiny.Health.Extensions.AI;

/// <summary>
/// Operations the LLM may perform on an opted-in health area. Read tools query data;
/// Write tools record data. Real-time observation is not exposed (tools are request/response).
/// </summary>
[Flags]
public enum HealthAICapabilities
{
    /// <summary>No tools are exposed for the health area.</summary>
    None = 0,
    /// <summary>Expose read tools that let the LLM query data for the health area.</summary>
    Read = 1 << 0,
    /// <summary>Expose write tools that let the LLM record data for the health area.</summary>
    Write = 1 << 1,
    /// <summary>Expose both read and write tools for the health area.</summary>
    ReadWrite = Read | Write
}
