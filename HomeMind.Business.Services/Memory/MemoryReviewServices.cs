using System.Text.Json;
using HomeMind.Business.IServices.Memory;
using HomeMind.Common.Model.Agent;
using HomeMind.Common.Model.Entities.Memory;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Memory;

/// <summary>
/// Creates pending review candidates only from the explicit <c>memoryCandidates</c> array in a completed Run result.
/// It never derives memories from Prompt, conversation text, or a free-form result summary.
/// </summary>
public sealed class MemoryReviewServices : IMemoryReviewServices
{
    private const int MaximumCandidatesPerRun = 10;
    private const int MaximumValueLength = 4_000;
    private readonly HomeMindDbContext _db;

    /// <summary>Creates the review consumer.</summary>
    public MemoryReviewServices(HomeMindDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<int> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var run = await _db.AgentRuns
            .Where(x => x.Status == AgentRunStatus.Completed && x.Result != null)
            .Where(x => !_db.MemoryReviewReceipts.Any(receipt => receipt.SourceRunId == x.Id))
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null) return 0;

        var existingCandidateCount = await _db.MemoryCandidates.CountAsync(x => x.SourceRunId == run.Id, cancellationToken);
        if (existingCandidateCount > 0)
        {
            _db.MemoryReviewReceipts.Add(new MemoryReviewReceipt { SourceRunId = run.Id, CandidateCount = existingCandidateCount, ReviewedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            return 0;
        }

        var proposals = ReadProposals(run.Result!);
        var now = DateTime.UtcNow;
        foreach (var proposal in proposals)
        {
            _db.MemoryCandidates.Add(new MemoryCandidate
            {
                HomeId = run.TenantId,
                OwnerUserId = proposal.Visibility == MemoryVisibility.Personal ? run.UserId : null,
                SourceRunId = run.Id,
                Kind = proposal.Kind,
                Visibility = proposal.Visibility,
                Key = proposal.Key,
                ProposedValue = proposal.Value,
                DisplaySummary = proposal.Summary,
                Category = proposal.Category,
                Confidence = proposal.Confidence,
                EvidenceRefsJson = JsonSerializer.Serialize(new[] { new { type = "run", id = run.Id } }),
                RiskLevel = proposal.RiskLevel,
                Status = MemoryCandidateStatus.Pending,
                ExpiresAt = proposal.ExpiresAt,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        _db.MemoryReviewReceipts.Add(new MemoryReviewReceipt { SourceRunId = run.Id, CandidateCount = proposals.Count, ReviewedAt = now });
        await _db.SaveChangesAsync(cancellationToken);
        return proposals.Count;
    }

    private static IReadOnlyList<MemoryProposal> ReadProposals(string result)
    {
        try
        {
            using var document = JsonDocument.Parse(result);
            if (!document.RootElement.TryGetProperty("memoryCandidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array) return Array.Empty<MemoryProposal>();

            var proposals = new List<MemoryProposal>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (proposals.Count == MaximumCandidatesPerRun || candidate.ValueKind != JsonValueKind.Object) break;
                if (!TryReadProposal(candidate, out var proposal)) continue;
                if (seen.Add($"{proposal.Visibility}\n{proposal.Key}")) proposals.Add(proposal);
            }
            return proposals;
        }
        catch (JsonException)
        {
            return Array.Empty<MemoryProposal>();
        }
    }

    private static bool TryReadProposal(JsonElement candidate, out MemoryProposal proposal)
    {
        proposal = default!;
        var kind = ReadRequiredString(candidate, "kind", 32);
        var visibility = ReadRequiredString(candidate, "visibility", 16);
        var key = ReadRequiredString(candidate, "key", 256);
        var value = ReadRequiredString(candidate, "value", MaximumValueLength);
        var summary = ReadRequiredString(candidate, "summary", 512);
        var riskLevel = ReadRequiredString(candidate, "riskLevel", 4);
        if (kind is not ("preference" or "fact" or "decision")
            || visibility is not (MemoryVisibility.Personal or MemoryVisibility.Family)
            || riskLevel is not ("L1" or "L2" or "L3")
            || key is null || value is null || summary is null) return false;
        if (!candidate.TryGetProperty("confidence", out var confidenceValue)
            || !confidenceValue.TryGetDecimal(out var confidence)
            || confidence is < 0 or > 1) return false;

        var category = ReadOptionalString(candidate, "category", 32);
        if (category is not null && category is not ("property" or "wifi" or "repair" or "cleaning" or "insurance" or "travel" or "other")) return false;
        var expiresAt = candidate.TryGetProperty("expiresAt", out var expiryValue) && expiryValue.ValueKind == JsonValueKind.String && expiryValue.TryGetDateTime(out var parsedExpiry)
            ? parsedExpiry.ToUniversalTime() : (DateTime?)null;
        if (expiresAt.HasValue && expiresAt <= DateTime.UtcNow) return false;

        proposal = new MemoryProposal(kind, visibility, key, value, summary, category, confidence, riskLevel, expiresAt);
        return true;
    }

    private static string? ReadRequiredString(JsonElement candidate, string name, int maximumLength)
    {
        var value = ReadOptionalString(candidate, name, maximumLength);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadOptionalString(JsonElement candidate, string name, int maximumLength)
    {
        if (!candidate.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) || text.Length > maximumLength ? null : text;
    }

    private sealed record MemoryProposal(string Kind, string Visibility, string Key, string Value, string Summary, string? Category, decimal Confidence, string RiskLevel, DateTime? ExpiresAt);
}
