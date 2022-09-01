using System.Collections.Concurrent;
using DBreeze.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DBreeze.AspNet;

public record ProxyState<T>(T Instance, DateTimeOffset Created, TimeSpan Timeout);
public class DBreezeEngineProxy : IDBreezeEngineProxy, IDBreezeEngineProxyChildFactory
{
    private readonly ILogger<DBreezeEngineProxy> _logger;
    private readonly DBreezeConfiguration _configuration;
    private volatile DBreezeEngine? _engine;
    private readonly ManualResetEvent _manualReset = new(true);
    private bool _disposed = false;
    public ulong EngineCalls => _engineCalls;
    private ulong _engineCalls = 0U;
    public ulong EngineCreated => _engineCreated;
    private ulong _engineCreated = 0U;
    public ulong Transactions => _transactions;
    private ulong _transactions = 0U;
    private bool _disposeEngineIfIdle = false;
    private readonly ConcurrentDictionary<int, ProxyState<IScheme>> _nonDisposedSchemes = new();
    private readonly ConcurrentDictionary<int, ProxyState<IDBreezeEngineProxy>> _nonDisposedEngineProxies = new();

    public DBreezeEngineProxy(IOptions<DBreezeConfiguration> options, ILogger<DBreezeEngineProxy> logger)
    {
        _logger = logger;
        _configuration = options.Value;
    }
    private DBreezeEngine GetEngine(CancellationToken? ctx = null)
    {
        ThrowIfDisposed();
        using var source = ctx is null ? new CancellationTokenSource() : CancellationTokenSource.CreateLinkedTokenSource(ctx.Value);
        source.CancelAfter(TimeSpan.FromSeconds(15));
        var token = source.Token;
        try
        {
            WaitHandle.WaitAny(new []{token.WaitHandle, _manualReset});
            Interlocked.Increment(ref _engineCalls);
            _logger.LogDebug("Database Called {EngineCalls}", EngineCalls);
            token.ThrowIfCancellationRequested();
            if (_engine is null || !Open)
            {
                RemoveStaleProxies();
                _engine = new DBreezeEngine(_configuration);
                Interlocked.Increment(ref _engineCreated);
                _logger.LogDebug("Created new Database Connection {EngineCreated}", EngineCreated);
            }
            _manualReset.Set();
            return _engine;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "The Database Has an Error during Fetching");
            Thread.Sleep(100);
            return GetEngine(token);
        }
    }

    public Transaction GetTransaction(eTransactionTablesLockTypes tablesLockType,
        params string[] tables)
    {
        ThrowIfDisposed();
        Interlocked.Increment(ref _transactions);
        _logger.LogDebug("Creating Transaction {Transactions}", Transactions);
        return GetEngine().GetTransaction(tablesLockType, tables);
    }

    private Scheme GetScheme() => GetEngine().Scheme;
    public IScheme Scheme()
    {
        ThrowIfDisposed();
        lock (_nonDisposedSchemes)
        {
            var proxy = new SchemeProxy(this, GetScheme());
            _nonDisposedSchemes.TryAdd(proxy.GetHashCode(), new ProxyState<IScheme>(proxy, DateTimeOffset.Now, TimeSpan.FromMinutes(1)));
            return proxy;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _manualReset.WaitOne();
        _disposed = true;
        _engine?.Dispose();
        RemoveStaleProxies();
        _manualReset.Dispose();
    }
    public void Dispose(IScheme proxy)
    {
        lock (_nonDisposedSchemes)
        {
            var hashCode = proxy.GetHashCode();
            _nonDisposedSchemes.TryRemove(hashCode, out var result);
            LogDisposeResults(result, hashCode);
            DisposeEngineIfNotUsed();
        }
    }
    public void Dispose(IDBreezeEngineProxy proxy)
    {
        lock (_nonDisposedEngineProxies)
        {
            var hashCode = proxy.GetHashCode();
            _nonDisposedEngineProxies.TryRemove(hashCode, out var result);
            LogDisposeResults(result, hashCode);
            DisposeEngineIfNotUsed();
        }
    }

    private void LogDisposeResults<T>(ProxyState<T>? result, int hashCode)
    {
        if (result != null)
        {
            var msTime = (DateTimeOffset.Now - result.Created).TotalMilliseconds;
            _logger.LogDebug("Removed Proxy: {HashCode} - Time Running: {MsTime}", hashCode, msTime);
        }
        else
        {
            _logger.LogWarning("Removed untracked Proxy: {HashCode}", hashCode);
        }
    }
    private void DisposeEngineIfNotUsed()
    {
        if (_nonDisposedSchemes.IsEmpty && _nonDisposedEngineProxies.IsEmpty && _disposeEngineIfIdle)
        {
            _logger.LogDebug("Everything is Disposed, Closing Engine!");
            _engine?.Dispose();
        }
        else
        {
            var schemesNum = _nonDisposedSchemes.Count;
            var enginesNum = _nonDisposedEngineProxies.Count;
            _logger.LogDebug("Engine is still used, Engine Keeps Running - Schemes: {SchemesNum}, Engines: {EnginesNum}", schemesNum, enginesNum);
        }
    }
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException("This Proxy is disposed");
    }
    public IDBreezeEngineProxy CreateDisposableChild()
    {
        ThrowIfDisposed();
        lock (_nonDisposedEngineProxies)
        {
            var proxy = new ProxyChild(this);
            _nonDisposedEngineProxies.TryAdd(proxy.GetHashCode(), new ProxyState<IDBreezeEngineProxy>(proxy, DateTimeOffset.Now, TimeSpan.FromMinutes(1)));
            return proxy;
        }
    }
    /// <summary>
    /// Check if Engine is Open for Operations
    /// </summary>
    /// <remarks>
    /// NOT THREAD SAFE
    /// </remarks>
    public bool Open => _engine is { Disposed: false, DBisOperable: true };

    public void RemoveStaleProxies()
    {
        lock (_nonDisposedSchemes)
        {
            if (Open)
            {
                var entries = FindStaleProxies(_nonDisposedSchemes);
                foreach (var entry in entries)
                {
                    _logger.LogWarning("Removed Overdue Proxy! Check For indisposed SchemeProxies in your Code!!");
                    _nonDisposedSchemes.TryRemove(entry.Key, out _);
                }
            }
            else if(!_nonDisposedSchemes.IsEmpty)
            {
                _logger.LogWarning("Engine Is Not Open For Operations, All Proxies Are Stale! Check For indisposed SchemeProxies in your Code!!");
                _nonDisposedSchemes.Clear();
            }
        }
        lock (_nonDisposedEngineProxies)
        {
            if (Open)
            {
                var entries = FindStaleProxies(_nonDisposedEngineProxies);
                foreach (var entry in entries)
                {
                    _nonDisposedEngineProxies.TryRemove(entry.Key, out _);
                    _logger.LogWarning("Removed Overdue Proxy! Check For indisposed EngineProxies in your Code!!");
                }
            }
            else if(!_nonDisposedEngineProxies.IsEmpty)
            {
                _logger.LogWarning("Engine Is Not Open For Operations, All Proxies Are Stale! Check For indisposed EngineProxies in your Code!!");
                _nonDisposedEngineProxies.Clear();
            }
        }
        DisposeEngineIfNotUsed();
    }

    static IEnumerable<KeyValuePair<int, ProxyState<T>>> FindStaleProxies<T>(ConcurrentDictionary<int, ProxyState<T>> proxies)
        where T : IDisposable
    {
        var currentTime = DateTimeOffset.Now;
        return from entry in proxies
            let state = entry.Value
            let processTime = currentTime - state.Created
            where processTime > state.Timeout
                select entry;
    }
    private class ProxyChild : IDBreezeEngineProxy
    {
        private readonly DBreezeEngineProxy _proxy;
        private readonly IScheme _scheme;
        private bool _disposed = false;
        public ProxyChild(DBreezeEngineProxy proxy)
        {
            _proxy = proxy;
            _scheme = _proxy.Scheme();
        }

        ~ProxyChild()
        {
            if (!_disposed)
            {
                _proxy.Dispose(this);
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scheme.Dispose();
            _proxy.Dispose(this);
        }
        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("This Proxy is disposed");
        }
        public Transaction GetTransaction(eTransactionTablesLockTypes tablesLockType, params string[] tables)
        {
            ThrowIfDisposed();
            return _proxy.GetTransaction(tablesLockType, tables);
        }

        public IScheme Scheme()
        {
            ThrowIfDisposed();
            return _scheme;
        }
    }
}