namespace DBreeze.AspNet;

internal class SchemeProxy : IScheme
{
    private readonly DBreezeEngineProxy _engine;
    private readonly Scheme _scheme;
    private bool _disposed = false;
    public SchemeProxy(DBreezeEngineProxy engine, Scheme scheme)
    {
        _engine = engine;
        _scheme = scheme;
    }

    ~SchemeProxy()
    {
        if (!_disposed)
        {
            _engine.Dispose(this);
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.Dispose(this);
    }
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException("This Proxy is disposed");
    }
    public string GetTablePathFromTableName(string userTableName)
    {
        ThrowIfDisposed();
        return _scheme.GetTablePathFromTableName(userTableName);
    }

    public bool IfUserTableExists(string userTableName)
    {
        ThrowIfDisposed();
        return _scheme.IfUserTableExists(userTableName);
    }

    public List<string> GetUserTableNamesStartingWith(string mask)
    {
        ThrowIfDisposed();
        return _scheme.GetUserTableNamesStartingWith(mask);
    }

    public void DeleteTable(string userTableName)
    {
        ThrowIfDisposed();
        _scheme.DeleteTable(userTableName);
    }

    public void RenameTable(string oldUserTableName, string newUserTableName)
    {
        ThrowIfDisposed();
        _scheme.RenameTable(oldUserTableName, newUserTableName);
    }
}