namespace DBreeze.AspNet;

public interface IDBreezeEngineProxyChildFactory
{
    ulong EngineCalls { get; }
    ulong EngineCreated { get; }
    ulong Transactions { get; }
    public IDBreezeEngineProxy CreateDisposableChild();
    public bool Open { get; }
    public void RemoveStaleProxies();
}