using MachineService.Persistence;

namespace MachineService.Tests.Fakes;

public sealed class CollectingHistoryWriteQueue : IHistoryWriteQueue
{
    public List<HistoryWrite> Writes { get; } = [];
    public int Depth => Writes.Count;
    public long DroppedWrites => 0;

    public bool TryEnqueue(HistoryWrite write)
    {
        Writes.Add(write);
        return true;
    }
}
