using SQLite;

namespace EpubReader.Database;

/// <summary>
/// Represents a database connection and provides methods to interact with the application's data store.
/// </summary>
/// <remarks>The <see cref="Db"/> class manages the connection to a SQLite database, allowing for operations such
/// as retrieving, saving, updating, and removing data related to application settings and books. It ensures that
/// necessary tables are created upon initialization and provides methods to handle data persistence.</remarks>
public partial class Db : IDb
{
	bool isInitialized = false;
	static readonly string dbErrorMsg = "Database connection is not initialized.";
	static readonly string errorMsg = "Database connection is null. Ensure that the database is initialized.";
	public static string DbPath => Path.Combine(Util.FileService.SaveDirectory, "MyData.dataSource");
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(Db));
	SQLiteAsyncConnection? conn;
	readonly SQLite.SQLiteOpenFlags flags =
		// open the database in read/write mode
		SQLite.SQLiteOpenFlags.ReadWrite |
		// create the database if it doesn't exist
		SQLite.SQLiteOpenFlags.Create |
		// enable multi-threaded database access
		SQLite.SQLiteOpenFlags.SharedCache;

	/// <summary>
	/// Initializes a new instance of the <see cref="Db"/> class, setting up the database connection and creating necessary
	/// tables.
	/// </summary>
	/// <remarks>This constructor ensures that the save directory exists before initializing the database
	/// connection. It creates the "Settings" and "Book" tables if they do not already exist.</remarks>
	public Db()
	{
		if (!Directory.Exists(Util.FileService.SaveDirectory))
		{
			Directory.CreateDirectory(Util.FileService.SaveDirectory);
		}
	}

	async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		if (isInitialized)
		{
			return;
		}
		conn ??= new SQLiteAsyncConnection(DbPath, flags);
		logger.Info("Database created");
		await conn.CreateTableAsync<Settings>().WaitAsync(cancellationToken);
		logger.Info("Settings Table created");
		await conn.CreateTableAsync<Book>().WaitAsync(cancellationToken);
		logger.Info("Book Table created");
		await EnsureSyncIdColumnAsync(cancellationToken);
		await BackfillBookSyncIdsAsync(cancellationToken);
		isInitialized = true;
	}
	/// <summary>
	/// Retrieves the current application settings from the database.
	/// </summary>
	/// <returns>The <see cref="Settings"/> object representing the current settings, or <see langword="null"/> if no settings are
	/// found.</returns>
	public async Task<Settings?> GetSettings(CancellationToken cancellationToken = default)
	{
		await InitializeAsync(cancellationToken);
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}
		return await conn.Table<Settings>().FirstOrDefaultAsync().WaitAsync(cancellationToken);
	}

	/// <summary>
	/// Retrieves a list of all books from the database.
	/// </summary>
	/// <returns>A list of <see cref="Book"/> objects representing all books in the database. The list will be empty if no books are
	/// found.</returns>
	public async Task<List<Book>> GetAllBooks(CancellationToken cancellationToken = default)
	{
		await InitializeAsync(cancellationToken);
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}
		var results = await conn.Table<Book>().ToListAsync().WaitAsync(cancellationToken) ?? [];
		foreach (var result in results)
		{
			await EnsureBookSyncIdAsync(result, cancellationToken);
		}
		return results;
	}

	/// <summary>
	/// Retrieves a book from the database that matches the specified book's ID.
	/// </summary>
	/// <param name="book">The book containing the ID to search for in the database.</param>
	/// <returns>The <see cref="Book"/> object with the matching ID, or <see langword="null"/> if no match is found.</returns>
	public async Task<Book?> GetBook(Book book, CancellationToken cancellationToken = default)
	{
		await InitializeAsync(cancellationToken);
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}
		var result = await conn.Table<Book>().FirstOrDefaultAsync(x => x.Id == book.Id).WaitAsync(cancellationToken);
		if (result is not null)
		{
			await EnsureBookSyncIdAsync(result, cancellationToken);
		}
		return result;
	}

	/// <summary>
	/// Saves the specified settings to the database.
	/// </summary>
	/// <remarks>If the settings with the specified <c>Id</c> already exist in the database, they are updated;
	/// otherwise, new settings are inserted.</remarks>
	/// <param name="settings">The settings to be saved. The settings object must have a valid <c>Id</c> property.</param>
	public async Task SaveSettings(Settings settings, CancellationToken cancellationToken = default)
	{
		await InitializeAsync(cancellationToken);
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}

		logger.Info("Inserting or updating settings");
		await conn.InsertOrReplaceAsync(settings).WaitAsync(cancellationToken);
	}

	/// <summary>
	/// Saves the specified book data to the database.
	/// </summary>
	/// <remarks>This method inserts a new book record into the database. It logs the insertion operation and
	/// ensures that no duplicate book entries are created.</remarks>
	/// <param name="book">The book object containing data to be saved. Must not be null and must have a unique identifier.</param>
	/// <exception cref="InvalidOperationException">Thrown if a book with the same identifier already exists in the database.</exception>
	public async Task SaveBookData(Book book, CancellationToken cancellationToken = default)
	{
		await InitializeAsync(cancellationToken);
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}

		book.SyncId = await BookIdentityService.ComputeSyncIdAsync(book, cancellationToken);

		var item = await conn.Table<Book>().FirstOrDefaultAsync(x => x.Id == book.Id).WaitAsync(cancellationToken);
		if (item is null)
		{
			logger.Info("Inserting book");
			await conn.InsertAsync(book).WaitAsync(cancellationToken);
			return;
		}
		logger.Info("Updating book");
		await conn.UpdateAsync(book).WaitAsync(cancellationToken);
	}

	/// <summary>
	/// Removes all settings from the data store.
	/// </summary>
	/// <remarks>This method deletes all entries of type <c>Settings</c> from the connected data store. Ensure that
	/// this operation is intended, as it cannot be undone.</remarks>
	public async Task RemoveAllSettings(CancellationToken cancellationToken = default)
	{
		await InitializeAsync(cancellationToken);
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}
		logger.Info("Removing all settings");
		await conn.DeleteAllAsync<Settings>().WaitAsync(cancellationToken);
	}

	/// <summary>
	/// Removes the specified book from the collection.
	/// </summary>
	/// <remarks>This method logs the removal operation and deletes the book from the database. Ensure that the book
	/// exists in the collection before calling this method.</remarks>
	/// <param name="book">The book to be removed. Cannot be null.</param>
	public async Task RemoveBook(Book book, CancellationToken cancellationToken = default)
	{
		await InitializeAsync(cancellationToken);
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}
		logger.Info("Removing book");
		await conn.DeleteAsync(book).WaitAsync(cancellationToken);
	}

	/// <summary>
	/// Removes all books from the database.
	/// </summary>
	/// <remarks>This method deletes all entries of type <see cref="Book"/> from the database. Ensure that this
	/// operation is intended, as it cannot be undone.</remarks>
	public async Task RemoveAllBooks(CancellationToken cancellationToken = default)
	{
		await InitializeAsync(cancellationToken);
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}
		logger.Info("Removing all books");
		await conn.DeleteAllAsync<Book>().WaitAsync(cancellationToken);
	}

	async Task EnsureSyncIdColumnAsync(CancellationToken cancellationToken)
	{
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}

		var tableInfo = await conn.GetTableInfoAsync(nameof(Book)).WaitAsync(cancellationToken) ?? [];
		var hasSyncId = tableInfo.Any(column => column.Name.Equals("SyncId", StringComparison.OrdinalIgnoreCase));
		if (!hasSyncId)
		{
			await conn.ExecuteAsync("ALTER TABLE Book ADD COLUMN SyncId TEXT").WaitAsync(cancellationToken);
			logger.Info("SyncId column added to Book table");
		}
	}

	async Task BackfillBookSyncIdsAsync(CancellationToken cancellationToken)
	{
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}

		var books = await conn.Table<Book>().ToListAsync().WaitAsync(cancellationToken) ?? [];
		foreach (var book in books)
		{
			if (!string.IsNullOrWhiteSpace(book.SyncId))
			{
				continue;
			}

			book.SyncId = await BookIdentityService.ComputeSyncIdAsync(book, cancellationToken);
			await conn.UpdateAsync(book).WaitAsync(cancellationToken);
		}
	}

	async Task EnsureBookSyncIdAsync(Book book, CancellationToken cancellationToken)
	{
		if (conn is null)
		{
			logger.Error(errorMsg);
			throw new InvalidOperationException(dbErrorMsg);
		}

		if (string.IsNullOrWhiteSpace(book.SyncId))
		{
			book.SyncId = await BookIdentityService.ComputeSyncIdAsync(book, cancellationToken);
			await conn.UpdateAsync(book).WaitAsync(cancellationToken);
		}
	}
}
