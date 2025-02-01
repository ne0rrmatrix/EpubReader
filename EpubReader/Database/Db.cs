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
            return;
        }
        if(!File.Exists(DbPath))
        {
            Directory.CreateDirectory(Service.FileService.SaveDirectory);
        }
        db = new SQLiteAsyncConnection(DbPath, Flags);
        logger.Info("Database created");
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
		return await db.Table<Book>().ToListAsync().WaitAsync(cancellationToken) ?? [];
	}

	public async Task<Book> GetBook(string title, CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return new Book();
		}
		return await db.Table<Book>().FirstOrDefaultAsync(x => x.Title == title).WaitAsync(cancellationToken) ?? new Book();
	}
	
	public async Task SaveSettings(Settings settings, CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return;
		}
		var item = await db.Table<Settings>().FirstOrDefaultAsync().WaitAsync(cancellationToken);
		if (settings.Id == 0 && item is null)
		{
			logger.Info("Inserting settings");
			await db.InsertAsync(settings).WaitAsync(cancellationToken);
			return;
		}
		else
		{
			logger.Info("Updating settings");
			await db.UpdateAsync(settings).WaitAsync(cancellationToken);
		}
	}

	public async Task SaveBookData(Book book, CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return;
		}
		var item = await db.Table<Book>().FirstOrDefaultAsync(x => x.Title == book.Title).WaitAsync(cancellationToken);
		if (book.Id == 0 && item is null)
		{
			logger.Info("Inserting book");
			await db.InsertAsync(book).WaitAsync(cancellationToken);
			return;
		}
		else
		{
			logger.Info("Updating book");
			await db.UpdateAsync(book).WaitAsync(cancellationToken);
		}
	}

	public async Task RemoveAllSettings(CancellationToken cancellationToken = default)
	{
		await Init(cancellationToken);
		if (db is null)
		{
			logger.Error("DB is null");
			return;
		}
		logger.Info("Removing all settings");
		await db.DeleteAllAsync<Settings>().WaitAsync(cancellationToken);
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
		await db.DeleteAllAsync<Book>().WaitAsync(cancellationToken);
	}
}
