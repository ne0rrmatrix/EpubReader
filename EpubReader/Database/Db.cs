using EpubReader.Interfaces;
using EpubReader.Models;
using MetroLog;
using SQLite;

namespace EpubReader.Database;

/// <summary>
/// Represents a database connection and provides methods to interact with the application's data store.
/// </summary>
/// <remarks>The <see cref="Db"/> class manages the connection to a SQLite database, allowing for operations such
/// as retrieving, saving, updating, and removing data related to application settings and books. It ensures that
/// necessary tables are created upon initialization and provides methods to handle data persistence.</remarks>
public partial class Db : IDb, IDisposable
{
	public static string DbPath => Path.Combine(Util.FileService.SaveDirectory, "MyData.dataSource");
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(Db));
	readonly SQLiteConnection conn;
	readonly SQLiteConnectionString options;
	bool disposedValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="Db"/> class, setting up the database connection and creating necessary
	/// tables.
	/// </summary>
	/// <remarks>This constructor ensures that the save directory exists before initializing the database
	/// connection. It creates the "Settings" and "Book" tables if they do not already exist.</remarks>
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
	
	/// <summary>
	/// Retrieves the current application settings from the database.
	/// </summary>
	/// <returns>The <see cref="Settings"/> object representing the current settings, or <see langword="null"/> if no settings are
	/// found.</returns>
	public Settings? GetSettings()
	{
		var results = conn.Table<Settings>().ToList().FirstOrDefault();
		return results;
	}

	/// <summary>
	/// Retrieves a list of all books from the database.
	/// </summary>
	/// <returns>A list of <see cref="Book"/> objects representing all books in the database. The list will be empty if no books are
	/// found.</returns>
	public List<Book> GetAllBooks()
	{
		var results = conn.Table<Book>().ToList();
		return results;
	}

	/// <summary>
	/// Retrieves a book from the database that matches the specified book's ID.
	/// </summary>
	/// <param name="book">The book containing the ID to search for in the database.</param>
	/// <returns>The <see cref="Book"/> object with the matching ID, or <see langword="null"/> if no match is found.</returns>
	public Book? GetBook(Book book)
	{
		var result = conn.Table<Book>().ToList().Find(x => x.Id == book.Id);
		return result;
	}

	/// <summary>
	/// Saves the specified settings to the database.
	/// </summary>
	/// <remarks>If the settings with the specified <c>Id</c> already exist in the database, they are updated;
	/// otherwise, new settings are inserted.</remarks>
	/// <param name="settings">The settings to be saved. The settings object must have a valid <c>Id</c> property.</param>
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

	/// <summary>
	/// Saves the specified book data to the database.
	/// </summary>
	/// <remarks>This method inserts a new book record into the database. It logs the insertion operation and
	/// ensures that no duplicate book entries are created.</remarks>
	/// <param name="book">The book object containing data to be saved. Must not be null and must have a unique identifier.</param>
	/// <exception cref="InvalidOperationException">Thrown if a book with the same identifier already exists in the database.</exception>
	public void SaveBookData(Book book)
	{
		var item = conn.Table<Book>().ToList().Find(x => x.Id == book.Id);
		if (item is not null)
		{
			throw new InvalidOperationException("Book already exists");
		}
		logger.Info("Inserting book");
		conn.Insert(book);
	}

	/// <summary>
	/// Updates the bookmark for a specified book in the database.
	/// </summary>
	/// <remarks>If the book does not exist in the database, it will be inserted as a new entry. Otherwise, the
	/// existing book's bookmark will be updated.</remarks>
	/// <param name="book">The book object containing the updated bookmark information. The book must have a valid <see cref="Book.Id"/>.</param>
	public void UpdateBookMark(Book book)
	{
		var item = conn.Table<Book>().ToList().Find(x => x.Id == book.Id);
		if (item is null)
		{
			conn.Insert(book);
			logger.Info("Inserting book");
			return;
		}
		item.CurrentChapter = book.CurrentChapter;
		item.CurrentPage = book.CurrentPage;
		conn.Update(item);
		logger.Info("Updating book");
	}

	/// <summary>
	/// Removes all settings from the data store.
	/// </summary>
	/// <remarks>This method deletes all entries of type <c>Settings</c> from the connected data store. Ensure that
	/// this operation is intended, as it cannot be undone.</remarks>
	public void RemoveAllSettings()
	{
		logger.Info("Removing all settings");
		conn.DeleteAll<Settings>();
	}

	/// <summary>
	/// Removes the specified book from the collection.
	/// </summary>
	/// <remarks>This method logs the removal operation and deletes the book from the database. Ensure that the book
	/// exists in the collection before calling this method.</remarks>
	/// <param name="book">The book to be removed. Cannot be null.</param>
	public void RemoveBook(Book book)
	{
		logger.Info("Removing book");
		conn.Delete(book);
	}

	/// <summary>
	/// Removes all books from the database.
	/// </summary>
	/// <remarks>This method deletes all entries of type <see cref="Book"/> from the database. Ensure that this
	/// operation is intended, as it cannot be undone.</remarks>
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
