using System.Threading.Channels;

namespace MachineService.Persistence;

public sealed class HistoryWriteQueue : IHistoryWriteQueue
{
    public const int Capacity = 1000;
    private int _depth;
    private long _droppedWrites;
    private readonly Channel<HistoryWrite> _channel =
        Channel.CreateBounded<HistoryWrite>(
            new BoundedChannelOptions(Capacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            }
        );

    public bool TryEnqueue(HistoryWrite write)
    {
        Interlocked.Increment(ref _depth);
        if (!_channel.Writer.TryWrite(write))
        {
            Interlocked.Decrement(ref _depth);
            Interlocked.Increment(ref _droppedWrites);
            return false;
        }
        return true;
    }

    public int Depth => Volatile.Read(ref _depth);
    public long DroppedWrites => Interlocked.Read(ref _droppedWrites);

    internal async IAsyncEnumerable<HistoryWrite> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        await foreach (var write in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _depth);
            yield return write;
        }
    }
}
