namespace MachineService.Persistence;

public sealed class PersistenceHealthState
{
    private readonly object _sync = new();
    private long _failedWrites;
    private DateTimeOffset? _lastSuccess;
    private string? _lastError;

    public void RecordSuccess()
    {
        lock (_sync)
        {
            _lastSuccess = DateTimeOffset.UtcNow;
            _lastError = null;
        }
    }

    public void RecordFailure(Exception exception)
    {
        lock (_sync)
        {
            ++_failedWrites;
            _lastError = exception.Message;
        }
    }

    public (long FailedWrites, DateTimeOffset? LastSuccess, string? LastError) Snapshot()
    {
        lock (_sync) return (_failedWrites, _lastSuccess, _lastError);
    }
}
