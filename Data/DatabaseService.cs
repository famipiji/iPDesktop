using SQLite;
using iPDesktop.Models;

namespace iPDesktop.Data;

public class DatabaseService
{
    private SQLiteAsyncConnection _db = null!;

    public async Task InitAsync()
    {
        if (_db is not null)
            return;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "ipdesktop.db3");
        _db = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        await _db.CreateTableAsync<User>();
    }

    // Example generic helpers — add model-specific methods below

    public async Task<List<T>> GetAllAsync<T>() where T : new()
    {
        await InitAsync();
        return await _db.Table<T>().ToListAsync();
    }

    public async Task<int> SaveAsync<T>(T item)
    {
        await InitAsync();
        return await _db.InsertOrReplaceAsync(item);
    }

    public async Task<int> DeleteAsync<T>(T item)
    {
        await InitAsync();
        return await _db.DeleteAsync(item);
    }
}
