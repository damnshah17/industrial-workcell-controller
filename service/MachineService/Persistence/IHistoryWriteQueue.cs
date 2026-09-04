namespace MachineService.Persistence;

public interface IHistoryWriteQueue
{
    bool TryEnqueue(HistoryWrite write);
}
