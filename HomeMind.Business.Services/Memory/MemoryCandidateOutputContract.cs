using System.Text.Json;

namespace HomeMind.Business.Services.Memory;

/// <summary>Opt-in output contract for Experts that may propose memory candidates.</summary>
public static class MemoryCandidateOutputContract
{
    /// <summary>
    /// Returns the model instruction only when the Expert output schema explicitly declares a
    /// <c>memoryCandidates</c> property. This keeps memory extraction disabled by default.
    /// </summary>
    public static string? GetPromptInstruction(string? outputSchema)
    {
        if (!DeclaresCandidates(outputSchema)) return null;
        return """

            This Expert explicitly supports optional memory candidates. Keep the normal response fields required by its output schema. Only when the user has explicitly stated a durable, non-sensitive preference, fact, or decision, add `memoryCandidates`; otherwise omit it. Never infer from silence or include credentials, addresses, health, financial, security, identity, or private third-party information. Each item must be one of:
            {"kind":"preference|fact|decision","visibility":"personal|family","key":"stable.dot.key","value":"minimal value","summary":"safe user-facing summary","category":"property|wifi|repair|cleaning|insurance|travel|other","confidence":0.0,"riskLevel":"L1|L2|L3","expiresAt":"optional ISO-8601 UTC"}
            `memoryCandidates` must be an array with at most 10 items. It is review-only: do not claim that any memory was saved or accepted.
            """;
    }

    private static bool DeclaresCandidates(string? outputSchema)
    {
        if (string.IsNullOrWhiteSpace(outputSchema)) return false;
        try
        {
            using var document = JsonDocument.Parse(outputSchema);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("properties", out var properties)
                   && properties.ValueKind == JsonValueKind.Object
                   && properties.TryGetProperty("memoryCandidates", out var candidates)
                   && candidates.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
