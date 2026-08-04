using System.Threading.Channels;
using HomeMind.Business.IServices.SmartHome;

namespace HomeMind.Business.Services.SmartHome;

public sealed class ChannelConnectorSyncQueue : IConnectorSyncQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    public ValueTask EnqueueAsync(long jobId, CancellationToken cancellationToken = default) => _channel.Writer.WriteAsync(jobId, cancellationToken);
    public bool TryDequeue(out long jobId) => _channel.Reader.TryRead(out jobId);
}
