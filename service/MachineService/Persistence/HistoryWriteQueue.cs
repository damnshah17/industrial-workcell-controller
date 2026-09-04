using System.Threading.Channels;

namespace MachineService.Persistence;

public sealed class HistoryWriteQueue : IHistoryWriteQueue
{
    private readonly Channel<HistoryWrite> _channel =
        Channel.CreateBounded<HistoryWrite>(
            new BoundedChannelOptions(1000)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            }
        );

    public bool TryEnqueue(HistoryWrite write) =>
        _channel.Writer.TryWrite(write);

    internal IAsyncEnumerable<HistoryWrite> ReadAllAsync(
        CancellationToken cancellationToken
    ) => _channel.Reader.ReadAllAsync(cancellationToken);
}
