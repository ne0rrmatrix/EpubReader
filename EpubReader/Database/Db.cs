using EpubReader.Interfaces;
using EpubReader.Models;
using MetroLog;
using SQLite;

namespace EpubReader.Database;

public partial class Db : IDb
{
    public static string DbPath => Path.Combine(Service.FileService.SaveDirectory, "MyData.db");
    SQLiteAsyncConnection? db;

    public const SQLiteOpenFlags Flags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;
    static readonly ILogger logger = LoggerFactory.GetLogger(nameof(Db));

    public Db()
    {
    }

    async Task Init(CancellationToken cancellationToken = default)
    {
        if (db is not null)
        {
            logger.Info("DB is not null");
            return;
        }
        if(!File.Exists(DbPath))
        {
            Directory.CreateDirectory(Service.FileService.SaveDirectory);
        }
        db = new SQLiteAsyncConnection(DbPath, Flags);
        logger.Info("Database created");
        await db.CreateTableAsync<FileData>().WaitAsync(cancellationToken);
		logger.Info("FileData Table created");
		await db.CreateTableAsync<Settings>().WaitAsync(cancellationToken);
		logger.Info("Settings Table created");
    }

	public async Task<Settings> GetSettings(CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return new Settings();
		}
		return await db.Table<Settings>().FirstOrDefaultAsync().WaitAsync(cancellationToken) ?? new Settings();
	}

	public async Task<List<FileData>> GetFileData(CancellationToken cancellationToken = default)
    {
        await Init(cancellationToken);
        if (db is null)
        {
            logger.Error("DB is null");
            return [];
        }
        return await db.Table<FileData>().ToListAsync().WaitAsync(cancellationToken);
    }

    public async Task<FileData?> GetFileData(int id, CancellationToken cancellationToken = default)
    {
        await Init(cancellationToken);
        if (db is null)
        {
            logger.Error("DB is null");
            return null;
        }
        return await db.FindAsync<FileData>(id).WaitAsync(cancellationToken);
    }

	public async Task SaveSettings(Settings settings, CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return;
		}
		var item = await db.FindAsync<Settings>(settings.Id).WaitAsync(cancellationToken);
		if (item is not null)
		{
			logger.Info("Updating settings");
			await db.DeleteAsync(item).WaitAsync(cancellationToken);
			await db.InsertAsync(settings).WaitAsync(cancellationToken);
			return;
		}
		logger.Info("Inserting settings");
		logger.Info($"SystemMode: {settings.IsSystemMode}");
		await db.InsertAsync(settings).WaitAsync(cancellationToken);
	}

	public async Task SaveFileData(FileData fileData, CancellationToken cancellationToken = default)
    {
        await Init(cancellationToken);
        if (db is null)
        {
            logger.Error("DB is null");
            return;
        }
        var item = await db.FindAsync<FileData>(fileData.Id).WaitAsync(cancellationToken);
        if (item is not null)
        {
            logger.Info("Updating fileData");
            await db.DeleteAsync(item).WaitAsync(cancellationToken);
            await db.InsertAsync(fileData).WaitAsync(cancellationToken);
            return;
        }
        await db.InsertAsync(fileData).WaitAsync(cancellationToken);
        logger.Info("Inserting fileData");
    }

	public async Task RemoveSettingsData(int id, CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return;
		}
		var item = await db.FindAsync<Settings>(id).WaitAsync(cancellationToken);
		if (item is null)
		{
			logger.Error("settings Data is null");
			return;
		}
		await db.DeleteAsync(item).WaitAsync(cancellationToken);
	}
	public async Task RemoveFileData(Book book, CancellationToken cancellationToken = default)
    {
        await Init(cancellationToken);
        if (db is null)
        {
            logger.Error("DB is null");
            return;
        }
        var item = await db.Table<FileData>().FirstOrDefaultAsync(x => x.Title == book.Title).WaitAsync(cancellationToken);
        if (item is null)
        {
            logger.Error("FileData is null");
            return;
        }
        await db.DeleteAsync(item).WaitAsync(cancellationToken);
    }

    public async Task UpdateBook(FileData fileData, CancellationToken cancellationToken = default)
    {
        await Init(cancellationToken);
        if (db is null)
        {
            logger.Error("DB is null");
            return;
        }
        logger.Info("Updating fileData");
        await db.UpdateAsync(fileData).WaitAsync(cancellationToken);
    }
}
