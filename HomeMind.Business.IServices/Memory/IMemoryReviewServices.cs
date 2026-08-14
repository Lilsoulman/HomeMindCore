namespace HomeMind.Business.IServices.Memory;

/// <summary>Consumes completed runs and creates review-only memory candidates from their explicit structured output.</summary>
public interface IMemoryReviewServices
{
    /// <summary>Processes one eligible completed run without changing the source run outcome.</summary>
    Task<int> ProcessNextAsync(CancellationToken cancellationToken = default);
}
