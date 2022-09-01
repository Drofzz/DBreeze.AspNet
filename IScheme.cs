namespace DBreeze.AspNet;

public interface IScheme : IDisposable
{
    public string GetTablePathFromTableName(string userTableName);
    public bool IfUserTableExists(string userTableName);
    public List<string> GetUserTableNamesStartingWith(string mask);
    public void DeleteTable(string userTableName);
    public void RenameTable(string oldUserTableName, string newUserTableName);
}