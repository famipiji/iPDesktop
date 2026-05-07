using SQLite;
using iPDesktop.Models;

namespace iPDesktop.Data;

public class DatabaseService
{
    private SQLiteAsyncConnection _db = null!;

    public string GetDatabasePath() => Path.Combine(FileSystem.AppDataDirectory, "ipdesktop.db3");

    public async Task InitAsync()
    {
        if (_db is not null)
            return;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "ipdesktop.db3");
        _db = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        await _db.CreateTableAsync<User>();
        await _db.CreateTableAsync<Document>();
        await SeedAsync();
    }

    private async Task SeedAsync()
    {
        var existing = await _db.Table<User>().Where(u => u.Username == "admin").FirstOrDefaultAsync();
        if (existing is null)
            await _db.InsertAsync(new User { Username = "admin", Password = "password123" });
    }

    // --- User ---

    public async Task<User?> GetUserAsync(string username, string password)
    {
        await InitAsync();
        return await _db.Table<User>()
            .Where(u => u.Username == username && u.Password == password)
            .FirstOrDefaultAsync();
    }

    // --- Documents ---

    public async Task<Document?> GetDocumentByIdAsync(int id)
    {
        await InitAsync();
        return await _db.Table<Document>().Where(d => d.Id == id).FirstOrDefaultAsync();
    }

    public async Task<int> InsertDocumentAsync(Document doc)
    {
        await InitAsync();
        return await _db.InsertAsync(doc);
    }

    // Paginated fetch — keeps memory flat regardless of total document count
    public async Task<List<Document>> GetDocumentsAsync(int page = 0, int pageSize = 50)
    {
        await InitAsync();
        return await _db.Table<Document>()
            .OrderByDescending(d => d.UploadedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<Document>> SearchDocumentsAsync(string query, int page = 0, int pageSize = 50)
    {
        await InitAsync();
        var q = query.ToLower();
        return await _db.Table<Document>()
            .Where(d => d.FileName.ToLower().Contains(q) || d.FileType.ToLower().Contains(q))
            .OrderByDescending(d => d.UploadedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetDocumentCountAsync()
    {
        await InitAsync();
        return await _db.Table<Document>().CountAsync();
    }

    public async Task<int> DeleteDocumentAsync(Document doc)
    {
        await InitAsync();
        return await _db.DeleteAsync(doc);
    }

    // --- Generic ---

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
