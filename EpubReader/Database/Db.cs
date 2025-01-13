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
		await db.CreateTableAsync<Book>().WaitAsync(cancellationToken);
		logger.Info("Book Table created");
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

	public async Task<List<Book>> GetAllBooks(CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return [];
		}
		return await db.Table<Book>().ToListAsync().WaitAsync(cancellationToken);
	}

	public async Task<Book> GetBook(string title, CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return new Book();
		}
		return await db.Table<Book>().FirstOrDefaultAsync(x => x.Title == title).WaitAsync(cancellationToken);
	}

	public async Task<FileData?> GetFileData(CancellationToken cancellationToken = default)
    {
        await Init(cancellationToken);
		if (db is null)
        {
            logger.Error("DB is null");
            return null;
        }
		return await db.Table<FileData>().FirstOrDefaultAsync().WaitAsync(cancellationToken);
	}

	public async Task<List<FileData>> GetAllFileData(CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return [];
		}
		return await db.Table<FileData>().ToListAsync().WaitAsync(cancellationToken);
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

	public async Task SaveBookData(Book book, CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return;
		}
		var item = await db.FindAsync<Book>(book.Id).WaitAsync(cancellationToken);
		if (item is not null)
		{
			logger.Info("Updating book");
			await db.DeleteAsync(item).WaitAsync(cancellationToken);
			await db.InsertAsync(book).WaitAsync(cancellationToken);
			return;
		}
		await db.InsertAsync(book).WaitAsync(cancellationToken);
		logger.Info("Inserting book");
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

	public async Task RemoveFileData(FileData? fileData, CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return;
		}
		if (fileData is null)
		{
			logger.Error("Cannot remove file data. The supplied file data is null");
			return;
		}
		var item = await db.Table<FileData>().FirstOrDefaultAsync(x => x.Title == fileData.Title).WaitAsync(cancellationToken);
		if (item is null)
		{
			logger.Error("FileData is null");
			return;
		}
		await db.DeleteAsync(item).WaitAsync(cancellationToken);
	}
	public async Task RemoveAllSettings(CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return;
		}
		await db.DropTableAsync<Settings>().WaitAsync(cancellationToken);
		await db.CreateTableAsync<Settings>().WaitAsync(cancellationToken);
	}

	public async Task RemoveBook(Book book, CancellationToken cancellationToken = default)
    {
        await Init(cancellationToken);
		if (db is null)
        {
            logger.Error("DB is null");
            return;
        }
		var item = await db.Table<Book>().FirstOrDefaultAsync(x => x.Title == book.Title).WaitAsync(cancellationToken);
		if (item is null)
		{
			logger.Error("FileData is null");
			return;
		}
		await db.DeleteAsync(item).WaitAsync(cancellationToken);
	}

	public async Task RemoveAllBooks(CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return;
		}
		await db.DropTableAsync<Book>().WaitAsync(cancellationToken);
		await db.CreateTableAsync<Book>().WaitAsync(cancellationToken);
	}
}
