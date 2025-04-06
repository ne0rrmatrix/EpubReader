using EpubReader.Interfaces;
using EpubReader.Models;
using MetroLog;
using SQLite;

namespace EpubReader.Database;

public partial class Db : IDb, IDisposable
{
	public static string DbPath => Path.Combine(Util.FileService.SaveDirectory, "MyData.dataSource");
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(Db));
	readonly SQLiteConnection conn;
	readonly SQLiteConnectionString options;
	bool disposedValue;

	public Db()
	{
		if(!Directory.Exists(Util.FileService.SaveDirectory))
		{
			Directory.CreateDirectory(Util.FileService.SaveDirectory);
		}
		options = new(DbPath, false);
		conn = new SQLiteConnection(options);
		logger.Info("Database created");
		conn.CreateTable<Settings>();
		logger.Info("Settings Table created");
		conn.CreateTable<Book>();
		logger.Info("Book Table created");
	}
	

	public Settings? GetSettings()
	{
		var results = conn.Table<Settings>().ToList().FirstOrDefault();
		return results;
	}

	public List<Book> GetAllBooks()
	{
		var results = conn.Table<Book>().ToList();
		return results;
	}

	public Book? GetBook(Book book)
	{
		var result = conn.Table<Book>().ToList().Find(x => x.Id == book.Id);
		return result;
	}

	public void SaveSettings(Settings settings)
	{
		var item = conn.Table<Settings>().ToList().Exists(x => x.Id == settings.Id);
		if (item)
		{
			logger.Info("Updating settings");
			conn.Update(settings);
			return;
		}
		logger.Info("Inserting settings");
		conn.Insert(settings);
	}

	public void SaveBookData(Book book)
	{
		var item = conn.Table<Book>().ToList().Find(x => x.Title == book.Title);
		if (item is not null)
		{
			throw new InvalidOperationException("Book already exists");
		}
		logger.Info("Inserting book");
		conn.Insert(book);
	}

	public void UpdateBookMark(Book book)
	{
		var item = conn.Table<Book>().ToList().Find(x => x.Title == book.Title) ?? throw new InvalidOperationException("Book not found");
		item.CurrentChapter = book.CurrentChapter;
		conn.Update(item);
		logger.Info("Updating book");
	}

	public void RemoveAllSettings()
	{
		logger.Info("Removing all settings");
		conn.DeleteAll<Settings>();
	}

	public void RemoveBook(Book book)
	{
		logger.Info("Removing book");
		conn.Delete(book);
	}

	public void RemoveAllBooks()
	{
		logger.Info("Removing all books");
		conn.DeleteAll<Book>();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing && conn is not null)
			{
				conn.Close();
				conn.Dispose();
				logger.Info("Database closed");
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
