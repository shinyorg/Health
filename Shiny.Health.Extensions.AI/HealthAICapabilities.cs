namespace Shiny.Health.Extensions.AI;

/// <summary>
/// Operations the LLM may perform on an opted-in health area. Read tools query data;
/// Write tools record data. Real-time observation is not exposed (tools are request/response).
/// </summary>
[Flags]
public enum HealthAICapabilities
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    ReadWrite = Read | Write
}
