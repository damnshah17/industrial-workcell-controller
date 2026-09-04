using MachineService.Persistence;

namespace MachineService.Tests.Fakes;

public sealed class CollectingHistoryWriteQueue : IHistoryWriteQueue
{
    public List<HistoryWrite> Writes { get; } = [];

    public bool TryEnqueue(HistoryWrite write)
    {
        Writes.Add(write);
        return true;
    }
}
