using DBreeze.Transactions;

namespace DBreeze.AspNet;

public interface IDBreezeEngineProxy : IDisposable
{
    /// <summary>Returns transaction object.</summary>
    /// <param name="tablesLockType">
    /// <para>SHARED: threads can use listed tables in parallel. Must be used together with tran.SynchronizeTables command, if necessary.</para>
    /// <para>EXCLUSIVE: if other threads use listed tables for reading or writing, current thread will be in a waiting queue.</para>
    /// </param>
    /// <param name="tables"></param>
    /// <returns>Returns transaction object</returns>
    Transaction GetTransaction(eTransactionTablesLockTypes tablesLockType,
        params string[] tables);

    /// <summary>Returns DBreeze schema object</summary>
    IScheme Scheme();
}