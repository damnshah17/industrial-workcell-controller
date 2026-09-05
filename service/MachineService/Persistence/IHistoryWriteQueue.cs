namespace MachineService.Persistence;

public interface IHistoryWriteQueue
{
    bool TryEnqueue(HistoryWrite write);
    int Depth { get; }
    long DroppedWrites { get; }
}
