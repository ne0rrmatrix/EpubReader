using EpubReader.Interfaces;
using EpubReader.Models;
using MetroLog;
using SQLite;

namespace EpubReader.Database;

public partial class Db : IDb, IDisposable
{
	public static string DbPath => Path.Combine(Util.FileService.SaveDirectory, "MyData.db");
	readonly SQLiteConnection db;
	bool disposedValue;
	public const SQLiteOpenFlags Flags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(Db));

	public Db()
	{
		if(!Directory.Exists(Util.FileService.SaveDirectory))
		{
			Directory.CreateDirectory(Util.FileService.SaveDirectory);
		}
		db = new SQLiteConnection(DbPath, Flags);
		logger.Info("Database created");
		db.CreateTable<Settings>();
		logger.Info("Settings Table created");
		db.CreateTable<Book>();
		logger.Info("Book Table created");
	}
	

	public Settings? GetSettings()
	{
		if (db == null)
		{
			logger.Info("Database is null");
			return null;
		}
		return db.Table<Settings>().FirstOrDefault() ?? new();
	}

	public List<Book>? GetAllBooks()
	{
		if(db is null)
		{
			logger.Info("Database is null");
			return null;
		}
		return db?.Table<Book>().ToList();
	}

	public Book? GetBook(string title)
	{
		if (db is null)
		{
			logger.Info("Database is null");
			return null;
		}
		return db.Table<Book>().Where(x => x.Title == title).FirstOrDefault();
	}

	public void SaveSettings(Settings settings)
	{
		if (db is null)
		{
			return;
		}
		var item = db.Table<Settings>().FirstOrDefault();
		if (item is not null)
		{
			logger.Info("Inserting settings");
			db.Delete(item);
			db.Insert(settings);
			return;
		}

		logger.Info("Updating settings");
		db.Insert(settings);
	}

	public void SaveBookData(Book book)
	{
		var item = db.Table<Book>().Where(x => x.Title == book.Title).FirstOrDefault();
		if (item is null)
		{
			logger.Info("Inserting book");
			db.Insert(book);
			return;
		}

		logger.Info("Updating book");
		db.Delete(item);
		db.Insert(book);
	}

	public void RemoveAllSettings()
	{
		logger.Info("Removing all settings");
		db.DeleteAll<Settings>();
	}

	public void RemoveBook(Book book)
	{
		var item = db.Table<Book>().Where(x => x.Title == book.Title);
		if (item is null)
		{
			return;
		}

		logger.Info("Removing book");
		db.Delete(item);
	}

	public void RemoveAllBooks()
	{
		logger.Info("Removing all books");
		db.DeleteAll<Book>();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				db?.Dispose();
			}
			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
