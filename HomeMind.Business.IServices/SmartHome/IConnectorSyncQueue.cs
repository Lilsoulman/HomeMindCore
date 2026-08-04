namespace HomeMind.Business.IServices.SmartHome;

/// <summary>Durable connector work is persisted first, then signalled through the in-process channel.</summary>
public interface IConnectorSyncQueue
{
    ValueTask EnqueueAsync(long jobId, CancellationToken cancellationToken = default);
    bool TryDequeue(out long jobId);
}
