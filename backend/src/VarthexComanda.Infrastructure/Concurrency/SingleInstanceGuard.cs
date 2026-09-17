namespace VarthexComanda.Infrastructure.Concurrency;

public class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _acquired;

    public SingleInstanceGuard(string mutexName)
    {
        _mutex = new Mutex(initiallyOwned: false, name: $"Global\\{mutexName}");
    }

    public bool TryAcquire()
    {
        try
        {
            _acquired = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            _acquired = true;
        }
        return _acquired;
    }

    public void Release()
    {
        if (_acquired)
        {
            _mutex.ReleaseMutex();
            _acquired = false;
        }
    }

    public void Dispose()
    {
        Release();
        _mutex.Dispose();
    }
}
