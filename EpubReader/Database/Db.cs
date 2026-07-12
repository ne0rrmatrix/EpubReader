using System.Globalization;
using Microsoft.Data.Sqlite;

namespace EpubReader.Database;

/// <summary>
/// Represents a database connection and provides methods to interact with the application's data store.
/// </summary>
/// <remarks>The <see cref="Db"/> class manages the connection to a SQLite database, allowing for operations such
/// as retrieving, saving, updating, and removing data related to application settings and books. It ensures that
/// necessary tables are created upon initialization and provides methods to handle data persistence.</remarks>
public partial class Db : IDb, IDisposable
{
	bool isInitialized = false;
	bool disposedValue;
	const string textColumnType = "TEXT";
	const string integerColumnType = "INTEGER";
	const string realColumnType = "REAL";
	const string dateTimeColumnType = "DATETIME";
	public static string DbPath => Path.Combine(Util.FileService.SaveDirectory, "MyData.dataSource");
	static readonly ILogger logger = AppLogger.CreateLogger<Db>();
	readonly SemaphoreSlim initLock = new(1, 1);

	static string ConnectionString => new SqliteConnectionStringBuilder
	{
		DataSource = DbPath,
		Mode = SqliteOpenMode.ReadWriteCreate,
		Cache = SqliteCacheMode.Shared
	}.ToString();

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

	async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		var conn = new SqliteConnection(ConnectionString);
		await conn.OpenAsync(cancellationToken);
		return conn;
	}

	async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (isInitialized)
		{
			return;
		}

		await initLock.WaitAsync(cancellationToken);
		try
		{
			if (isInitialized)
			{
				return;
			}

			using var conn = new SqliteConnection(ConnectionString);
			await conn.OpenAsync(cancellationToken);
			logger.Info("Database created");

			await CreateTablesAsync(conn, cancellationToken);
			await EnsureSettingsColumnsAsync(conn, cancellationToken);
			await EnsureSyncIdColumnAsync(conn, cancellationToken);
			await BackfillBookSyncIdsAsync(conn, cancellationToken);
			await EnsureBookMediaOverlayColumnsAsync(conn, cancellationToken);
			await EnsureLastOpenedDateColumnAsync(conn, cancellationToken);
			isInitialized = true;
		}
		finally
		{
			initLock.Release();
		}
	}

	static async Task CreateTablesAsync(SqliteConnection conn, CancellationToken cancellationToken)
	{
		var cmd = conn.CreateCommand();
		cmd.CommandText = """
			CREATE TABLE IF NOT EXISTS settings (
				Id TEXT PRIMARY KEY NOT NULL,
				FontFamily TEXT NOT NULL DEFAULT '',
				FontSize INTEGER NOT NULL DEFAULT 16,
				LineSpacing TEXT NOT NULL DEFAULT '1.5',
				TextAlignment TEXT NOT NULL DEFAULT '',
				ParagraphSpacing TEXT NOT NULL DEFAULT '',
				BodyHyphens TEXT NOT NULL DEFAULT '',
				LetterSpacing TEXT NOT NULL DEFAULT '',
				WordSpacing TEXT NOT NULL DEFAULT '',
				BackgroundColor TEXT NOT NULL DEFAULT '',
				TextColor TEXT NOT NULL DEFAULT '',
				ColorScheme TEXT NOT NULL DEFAULT '',
				SupportMultipleColumns INTEGER NOT NULL DEFAULT 0,
				CalibreAutoDiscovery INTEGER NOT NULL DEFAULT 1,
				Port INTEGER NOT NULL DEFAULT 8080,
				IPAddress TEXT NOT NULL DEFAULT 'localhost',
				UrlPrefix TEXT NOT NULL DEFAULT 'http',
				CalibreManualPort INTEGER NOT NULL DEFAULT 8080,
				CalibreManualIPAddress TEXT NOT NULL DEFAULT 'localhost',
				CalibreManualUrlPrefix TEXT NOT NULL DEFAULT 'http'
			)
			""";
		await cmd.ExecuteNonQueryAsync(cancellationToken);
		logger.Info("Settings Table created");

		cmd.CommandText = """
			CREATE TABLE IF NOT EXISTS Book (
				Id TEXT PRIMARY KEY NOT NULL,
				Title TEXT NOT NULL DEFAULT '',
				FilePath TEXT NOT NULL DEFAULT '',
				CurrentChapter INTEGER NOT NULL DEFAULT 0,
				CurrentPage INTEGER NOT NULL DEFAULT 0,
				MediaOverlayEnabled INTEGER,
				MediaOverlayChapter INTEGER,
				MediaOverlaySegmentIndex INTEGER,
				MediaOverlayPositionSeconds REAL,
				MediaOverlayFragmentId TEXT,
				CoverImagePath TEXT NOT NULL DEFAULT '',
				SyncId TEXT NOT NULL DEFAULT '',
				Author TEXT NOT NULL DEFAULT '',
				IsInLibrary INTEGER NOT NULL DEFAULT 0,
				DateAdded DATETIME NOT NULL,
				LastOpenedDate DATETIME
			)
			""";
		await cmd.ExecuteNonQueryAsync(cancellationToken);
		logger.Info("Book Table created");
	}

	static async Task EnsureSettingsColumnsAsync(SqliteConnection conn, CancellationToken cancellationToken)
	{
		await EnsureColumnsAsync(
			conn,
			"settings",
			[("LineSpacing", textColumnType), ("TextAlignment", textColumnType), ("ParagraphSpacing", textColumnType), ("BodyHyphens", textColumnType), ("LetterSpacing", textColumnType), ("WordSpacing", textColumnType), ("CalibreManualPort", integerColumnType), ("CalibreManualIPAddress", textColumnType), ("CalibreManualUrlPrefix", textColumnType)],
			cancellationToken);
	}

	static async Task EnsureLastOpenedDateColumnAsync(SqliteConnection conn, CancellationToken cancellationToken)
	{
		await EnsureColumnsAsync(
			conn,
			"Book",
			[("LastOpenedDate", dateTimeColumnType)],
			cancellationToken);
	}

	static async Task EnsureBookMediaOverlayColumnsAsync(SqliteConnection conn, CancellationToken cancellationToken)
	{
		// Keep schema in sync with Models/Book.cs optional MediaOverlay fields.
		await EnsureColumnsAsync(
			conn,
			"Book",
			[
				("MediaOverlayEnabled", integerColumnType),
				("MediaOverlayChapter", integerColumnType),
				("MediaOverlaySegmentIndex", integerColumnType),
				("MediaOverlayPositionSeconds", realColumnType),
				("MediaOverlayFragmentId", textColumnType)
			],
			cancellationToken);
	}

	static async Task EnsureColumnsAsync(
		SqliteConnection conn,
		string tableName,
		IReadOnlyList<(string ColumnName, string SqlType)> columns,
		CancellationToken cancellationToken)
	{
		var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		using var pragmaCmd = conn.CreateCommand();
#pragma warning disable S2077
		pragmaCmd.CommandText = $"PRAGMA table_info({tableName})";
#pragma warning restore S2077
		using var reader = await pragmaCmd.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			existingNames.Add(reader.GetString(1)); // column name is at index 1 in PRAGMA table_info
		}

		foreach ((string? columnName, string? sqlType) in columns)
		{
			if (existingNames.Contains(columnName))
			{
				continue;
			}

			using var alterCmd = conn.CreateCommand();
#pragma warning disable S2077
			alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {sqlType}";
#pragma warning restore S2077
			await alterCmd.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	/// <summary>
	/// Retrieves the current application settings from the database.
	/// </summary>
	/// <returns>The <see cref="Settings"/> object representing the current settings, or <see langword="null"/> if no settings are
	/// found.</returns>
	public async Task<Settings?> GetSettings(CancellationToken cancellationToken = default)
	{
		using var conn = await OpenConnectionAsync(cancellationToken);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT Id, FontFamily, FontSize, LineSpacing, TextAlignment, ParagraphSpacing, BodyHyphens, LetterSpacing, WordSpacing, BackgroundColor, TextColor, ColorScheme, SupportMultipleColumns, CalibreAutoDiscovery, Port, IPAddress, UrlPrefix, CalibreManualPort, CalibreManualIPAddress, CalibreManualUrlPrefix FROM settings LIMIT 1";
		using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
		if (await reader.ReadAsync(cancellationToken))
		{
			return ReadSettings(reader);
		}
		return null;
	}

	/// <summary>
	/// Retrieves a list of all books from the database.
	/// </summary>
	/// <returns>A list of <see cref="Book"/> objects representing all books in the database. The list will be empty if no books are
	/// found.</returns>
	public async Task<List<Book>> GetAllBooks(CancellationToken cancellationToken = default)
	{
		using var conn = await OpenConnectionAsync(cancellationToken);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT Id, Title, FilePath, CurrentChapter, CurrentPage, MediaOverlayEnabled, MediaOverlayChapter, MediaOverlaySegmentIndex, MediaOverlayPositionSeconds, MediaOverlayFragmentId, CoverImagePath, SyncId, Author, IsInLibrary, DateAdded, LastOpenedDate FROM Book";
		using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
		var results = new List<Book>();
		while (await reader.ReadAsync(cancellationToken))
		{
			Book book = ReadBook(reader);
			await EnsureBookSyncIdAsync(conn, book, cancellationToken);
			results.Add(book);
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
		using var conn = await OpenConnectionAsync(cancellationToken);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT Id, Title, FilePath, CurrentChapter, CurrentPage, MediaOverlayEnabled, MediaOverlayChapter, MediaOverlaySegmentIndex, MediaOverlayPositionSeconds, MediaOverlayFragmentId, CoverImagePath, SyncId, Author, IsInLibrary, DateAdded, LastOpenedDate FROM Book WHERE Id = @id";
		cmd.Parameters.AddWithValue("@id", book.Id.ToString());
		using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
		if (await reader.ReadAsync(cancellationToken))
		{
			Book? result = ReadBook(reader);
			await EnsureBookSyncIdAsync(conn, result, cancellationToken);
			return result;
		}
		return null;
	}

	/// <summary>
	/// Saves the specified settings to the database.
	/// </summary>
	/// <remarks>If the settings with the specified <c>Id</c> already exist in the database, they are updated;
	/// otherwise, new settings are inserted.</remarks>
	/// <param name="settings">The settings to be saved. The settings object must have a valid <c>Id</c> property.</param>
	public async Task SaveSettings(Settings settings, CancellationToken cancellationToken = default)
	{
		using var conn = await OpenConnectionAsync(cancellationToken);
		logger.Info("Inserting or updating settings");
		using var cmd = conn.CreateCommand();
		cmd.CommandText = """
			INSERT OR REPLACE INTO settings (Id, FontFamily, FontSize, LineSpacing, TextAlignment, ParagraphSpacing, BodyHyphens, LetterSpacing, WordSpacing, BackgroundColor, TextColor, ColorScheme, SupportMultipleColumns, CalibreAutoDiscovery, Port, IPAddress, UrlPrefix, CalibreManualPort, CalibreManualIPAddress, CalibreManualUrlPrefix)
			VALUES (@id, @fontFamily, @fontSize, @lineSpacing, @textAlignment, @paragraphSpacing, @bodyHyphens, @letterSpacing, @wordSpacing, @backgroundColor, @textColor, @colorScheme, @supportMultipleColumns, @calibreAutoDiscovery, @port, @ipAddress, @urlPrefix, @calibreManualPort, @calibreManualIPAddress, @calibreManualUrlPrefix)
			""";
		BindSettingsParameters(cmd, settings);
		await cmd.ExecuteNonQueryAsync(cancellationToken);
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
		using var conn = await OpenConnectionAsync(cancellationToken);

		book.SyncId = await BookIdentityService.ComputeSyncIdAsync(book, cancellationToken);

		bool exists;
		using (var checkCmd = conn.CreateCommand())
		{
			checkCmd.CommandText = "SELECT COUNT(1) FROM Book WHERE Id = @id";
			checkCmd.Parameters.AddWithValue("@id", book.Id.ToString());
			exists = (long)(await checkCmd.ExecuteScalarAsync(cancellationToken))! > 0;
		}

		if (!exists)
		{
			logger.Info("Inserting book");
			using var insertCmd = conn.CreateCommand();
			insertCmd.CommandText = """
				INSERT INTO Book (Id, Title, FilePath, CurrentChapter, CurrentPage, MediaOverlayEnabled, MediaOverlayChapter, MediaOverlaySegmentIndex, MediaOverlayPositionSeconds, MediaOverlayFragmentId, CoverImagePath, SyncId, Author, IsInLibrary, DateAdded, LastOpenedDate)
				VALUES (@id, @title, @filePath, @currentChapter, @currentPage, @mediaOverlayEnabled, @mediaOverlayChapter, @mediaOverlaySegmentIndex, @mediaOverlayPositionSeconds, @mediaOverlayFragmentId, @coverImagePath, @syncId, @author, @isInLibrary, @dateAdded, @lastOpenedDate)
				""";
			BindBookParameters(insertCmd, book);
			await insertCmd.ExecuteNonQueryAsync(cancellationToken);
		}
		else
		{
			logger.Info("Updating book");
			using var updateCmd = conn.CreateCommand();
			updateCmd.CommandText = """
				UPDATE Book SET
					Title = @title,
					FilePath = @filePath,
					CurrentChapter = @currentChapter,
					CurrentPage = @currentPage,
					MediaOverlayEnabled = @mediaOverlayEnabled,
					MediaOverlayChapter = @mediaOverlayChapter,
					MediaOverlaySegmentIndex = @mediaOverlaySegmentIndex,
					MediaOverlayPositionSeconds = @mediaOverlayPositionSeconds,
					MediaOverlayFragmentId = @mediaOverlayFragmentId,
					CoverImagePath = @coverImagePath,
					SyncId = @syncId,
					Author = @author,
					IsInLibrary = @isInLibrary,
					DateAdded = @dateAdded,
					LastOpenedDate = @lastOpenedDate
				WHERE Id = @id
				""";
			BindBookParameters(updateCmd, book);
			await updateCmd.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	/// <summary>
	/// Updates only the CurrentChapter and CurrentPage columns for the specified book.
	/// </summary>
	public async Task UpdateBookProgress(Guid bookId, int currentChapter, int currentPage, CancellationToken cancellationToken = default)
	{
		using var conn = await OpenConnectionAsync(cancellationToken);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "UPDATE Book SET CurrentChapter = @currentChapter, CurrentPage = @currentPage WHERE Id = @id";
		cmd.Parameters.AddWithValue("@currentChapter", currentChapter);
		cmd.Parameters.AddWithValue("@currentPage", currentPage);
		cmd.Parameters.AddWithValue("@id", bookId.ToString());
		await cmd.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>
	/// Updates only the last opened date column for the specified book.
	/// </summary>
	public async Task UpdateBookLastOpenedDate(Guid bookId, DateTime lastOpenedDate, CancellationToken cancellationToken = default)
	{
		using var conn = await OpenConnectionAsync(cancellationToken);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "UPDATE Book SET LastOpenedDate = @lastOpenedDate WHERE Id = @id";
		cmd.Parameters.AddWithValue("@lastOpenedDate", lastOpenedDate);
		cmd.Parameters.AddWithValue("@id", bookId.ToString());
		await cmd.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task UpdateBookMediaOverlayProgress(
		Guid bookId,
		bool? enabled,
		int? chapterIndex,
		int? segmentIndex,
		double? positionSeconds,
		string? fragmentId,
		CancellationToken cancellationToken = default)
	{
		using var conn = await OpenConnectionAsync(cancellationToken);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = """
			UPDATE Book
			SET
				MediaOverlayEnabled = @enabled,
				MediaOverlayChapter = @chapterIndex,
				MediaOverlaySegmentIndex = @segmentIndex,
				MediaOverlayPositionSeconds = @positionSeconds,
				MediaOverlayFragmentId = @fragmentId
			WHERE Id = @id
			""";
		cmd.Parameters.AddWithValue("@enabled", (object?)enabled ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@chapterIndex", (object?)chapterIndex ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@segmentIndex", (object?)segmentIndex ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@positionSeconds", (object?)positionSeconds ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@fragmentId", (object?)fragmentId ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@id", bookId.ToString());
		await cmd.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>
	/// Removes all settings from the data store.
	/// </summary>
	/// <remarks>This method deletes all entries of type <c>Settings</c> from the connected data store. Ensure that
	/// this operation is intended, as it cannot be undone.</remarks>
	public async Task RemoveAllSettings(CancellationToken cancellationToken = default)
	{
		using var conn = await OpenConnectionAsync(cancellationToken);
		logger.Info("Removing all settings");
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "DELETE FROM settings";
		await cmd.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>
	/// Removes the specified book from the collection.
	/// </summary>
	/// <remarks>This method logs the removal operation and deletes the book from the database. Ensure that the book
	/// exists in the collection before calling this method.</remarks>
	/// <param name="book">The book to be removed. Cannot be null.</param>
	public async Task RemoveBook(Book book, CancellationToken cancellationToken = default)
	{
		using var conn = await OpenConnectionAsync(cancellationToken);
		logger.Info("Removing book");
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "DELETE FROM Book WHERE Id = @id";
		cmd.Parameters.AddWithValue("@id", book.Id.ToString());
		await cmd.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>
	/// Removes all books from the database.
	/// </summary>
	/// <remarks>This method deletes all entries of type <see cref="Book"/> from the database. Ensure that this
	/// operation is intended, as it cannot be undone.</remarks>
	public async Task RemoveAllBooks(CancellationToken cancellationToken = default)
	{
		using var conn = await OpenConnectionAsync(cancellationToken);
		logger.Info("Removing all books");
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "DELETE FROM Book";
		await cmd.ExecuteNonQueryAsync(cancellationToken);
	}

	static async Task EnsureSyncIdColumnAsync(SqliteConnection conn, CancellationToken cancellationToken)
	{
		var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		using var pragmaCmd = conn.CreateCommand();
		pragmaCmd.CommandText = "PRAGMA table_info(Book)";
		using var reader = await pragmaCmd.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			existingNames.Add(reader.GetString(1));
		}

		if (!existingNames.Contains("SyncId"))
		{
			using var alterCmd = conn.CreateCommand();
			alterCmd.CommandText = "ALTER TABLE Book ADD COLUMN SyncId TEXT";
			await alterCmd.ExecuteNonQueryAsync(cancellationToken);
			logger.Info("SyncId column added to Book table");
		}
	}

	static async Task BackfillBookSyncIdsAsync(SqliteConnection conn, CancellationToken cancellationToken)
	{
		using var selectCmd = conn.CreateCommand();
		selectCmd.CommandText = "SELECT Id, SyncId FROM Book";
		using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
		var idsToBackfill = new List<Guid>();
		while (await reader.ReadAsync(cancellationToken))
		{
			string existingSyncId = await reader.IsDBNullAsync(1, cancellationToken) ? string.Empty : reader.GetString(1);
			if (string.IsNullOrWhiteSpace(existingSyncId))
			{
				idsToBackfill.Add(Guid.Parse(reader.GetString(0)));
			}
		}

		foreach (Guid id in idsToBackfill)
		{
			using var loadCmd = conn.CreateCommand();
			loadCmd.CommandText = "SELECT Id, Title, FilePath, CurrentChapter, CurrentPage, MediaOverlayEnabled, MediaOverlayChapter, MediaOverlaySegmentIndex, MediaOverlayPositionSeconds, MediaOverlayFragmentId, CoverImagePath, SyncId, Author, IsInLibrary, DateAdded, LastOpenedDate FROM Book WHERE Id = @id";
			loadCmd.Parameters.AddWithValue("@id", id.ToString());
			using var loadReader = await loadCmd.ExecuteReaderAsync(cancellationToken);
			if (await loadReader.ReadAsync(cancellationToken))
			{
				Book book = ReadBook(loadReader);
				book.SyncId = await BookIdentityService.ComputeSyncIdAsync(book, cancellationToken);
				using var updateCmd = conn.CreateCommand();
				updateCmd.CommandText = "UPDATE Book SET SyncId = @syncId WHERE Id = @id";
				updateCmd.Parameters.AddWithValue("@syncId", book.SyncId);
				updateCmd.Parameters.AddWithValue("@id", id.ToString());
				await updateCmd.ExecuteNonQueryAsync(cancellationToken);
			}
		}
	}

	static async Task EnsureBookSyncIdAsync(SqliteConnection conn, Book book, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(book.SyncId))
		{
			book.SyncId = await BookIdentityService.ComputeSyncIdAsync(book, cancellationToken);
			using var cmd = conn.CreateCommand();
			cmd.CommandText = "UPDATE Book SET SyncId = @syncId WHERE Id = @id";
			cmd.Parameters.AddWithValue("@syncId", book.SyncId);
			cmd.Parameters.AddWithValue("@id", book.Id.ToString());
			await cmd.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	// ── Mapping helpers ──

	static Settings ReadSettings(SqliteDataReader reader)
	{
		return new Settings
		{
			Id = Guid.Parse(reader.GetString(0)),
			FontFamily = reader.GetString(1),
			FontSize = reader.GetInt32(2),
			LineSpacing = reader.GetString(3),
			TextAlignment = reader.GetString(4),
			ParagraphSpacing = reader.GetString(5),
			BodyHyphens = reader.GetString(6),
			LetterSpacing = reader.GetString(7),
			WordSpacing = reader.GetString(8),
			BackgroundColor = reader.GetString(9),
			TextColor = reader.GetString(10),
			ColorScheme = reader.GetString(11),
			SupportMultipleColumns = reader.GetInt32(12) != 0,
			CalibreAutoDiscovery = reader.GetInt32(13) != 0,
			Port = reader.GetInt32(14),
			IPAddress = reader.GetString(15),
			UrlPrefix = reader.GetString(16),
			CalibreManualPort = reader.GetInt32(17),
			CalibreManualIPAddress = reader.GetString(18),
			CalibreManualUrlPrefix = reader.GetString(19)
		};
	}

	static Book ReadBook(SqliteDataReader reader)
	{
		return new Book
		{
			Id = Guid.Parse(reader.GetString(0)),
			Title = reader.GetString(1),
			FilePath = reader.GetString(2),
			CurrentChapter = reader.GetInt32(3),
			CurrentPage = reader.GetInt32(4),
			MediaOverlayEnabled = reader.IsDBNull(5) ? null : reader.GetBoolean(5),
			MediaOverlayChapter = reader.IsDBNull(6) ? null : reader.GetInt32(6),
			MediaOverlaySegmentIndex = reader.IsDBNull(7) ? null : reader.GetInt32(7),
			MediaOverlayPositionSeconds = reader.IsDBNull(8) ? null : reader.GetDouble(8),
			MediaOverlayFragmentId = reader.IsDBNull(9) ? null : reader.GetString(9),
			CoverImagePath = reader.GetString(10),
			SyncId = reader.GetString(11),
			Author = reader.GetString(12),
			IsInLibrary = reader.GetInt32(13) != 0,
			DateAdded = ReadDateTime(reader, 14),
			LastOpenedDate = reader.IsDBNull(15) ? null : ReadDateTime(reader, 15)
		};
	}

	/// <summary>
	/// Reads a DateTime from a column that may have been stored by sqlite-net-pcl (as ticks string)
	/// or by Microsoft.Data.Sqlite (as ISO 8601 / native format). Falls back to <see cref="DateTime.MinValue"/>
	/// when the column cannot be parsed.
	/// </summary>
	static DateTime ReadDateTime(SqliteDataReader reader, int ordinal)
	{
		if (reader.IsDBNull(ordinal))
		{
			return DateTime.MinValue;
		}

		// sqlite-net-pcl stores DateTime as a string of ticks (e.g. "638775360000000000")
		// Microsoft.Data.Sqlite stores it natively as an ISO 8601 string.
		string raw = reader.GetString(ordinal);
		if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
		{
			try
			{
				return new DateTime(ticks, DateTimeKind.Utc);
			}
			catch (ArgumentOutOfRangeException)
			{
				// Ticks value out of range — fall through to DateTime parse.
			}
		}

		if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
		{
			return parsed;
		}

		return DateTime.MinValue;
	}

	static void BindSettingsParameters(SqliteCommand cmd, Settings settings)
	{
		cmd.Parameters.AddWithValue("@id", settings.Id.ToString());
		cmd.Parameters.AddWithValue("@fontFamily", settings.FontFamily);
		cmd.Parameters.AddWithValue("@fontSize", settings.FontSize);
		cmd.Parameters.AddWithValue("@lineSpacing", settings.LineSpacing);
		cmd.Parameters.AddWithValue("@textAlignment", settings.TextAlignment);
		cmd.Parameters.AddWithValue("@paragraphSpacing", settings.ParagraphSpacing);
		cmd.Parameters.AddWithValue("@bodyHyphens", settings.BodyHyphens);
		cmd.Parameters.AddWithValue("@letterSpacing", settings.LetterSpacing);
		cmd.Parameters.AddWithValue("@wordSpacing", settings.WordSpacing);
		cmd.Parameters.AddWithValue("@backgroundColor", settings.BackgroundColor);
		cmd.Parameters.AddWithValue("@textColor", settings.TextColor);
		cmd.Parameters.AddWithValue("@colorScheme", settings.ColorScheme);
		cmd.Parameters.AddWithValue("@supportMultipleColumns", settings.SupportMultipleColumns ? 1 : 0);
		cmd.Parameters.AddWithValue("@calibreAutoDiscovery", settings.CalibreAutoDiscovery ? 1 : 0);
		cmd.Parameters.AddWithValue("@port", settings.Port);
		cmd.Parameters.AddWithValue("@ipAddress", settings.IPAddress);
		cmd.Parameters.AddWithValue("@urlPrefix", settings.UrlPrefix);
		cmd.Parameters.AddWithValue("@calibreManualPort", settings.CalibreManualPort);
		cmd.Parameters.AddWithValue("@calibreManualIPAddress", settings.CalibreManualIPAddress);
		cmd.Parameters.AddWithValue("@calibreManualUrlPrefix", settings.CalibreManualUrlPrefix);
	}

	static void BindBookParameters(SqliteCommand cmd, Book book)
	{
		cmd.Parameters.AddWithValue("@id", book.Id.ToString());
		cmd.Parameters.AddWithValue("@title", book.Title);
		cmd.Parameters.AddWithValue("@filePath", book.FilePath);
		cmd.Parameters.AddWithValue("@currentChapter", book.CurrentChapter);
		cmd.Parameters.AddWithValue("@currentPage", book.CurrentPage);
		cmd.Parameters.AddWithValue("@mediaOverlayEnabled", (object?)book.MediaOverlayEnabled ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlayChapter", (object?)book.MediaOverlayChapter ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlaySegmentIndex", (object?)book.MediaOverlaySegmentIndex ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlayPositionSeconds", (object?)book.MediaOverlayPositionSeconds ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlayFragmentId", (object?)book.MediaOverlayFragmentId ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@coverImagePath", book.CoverImagePath);
		cmd.Parameters.AddWithValue("@syncId", book.SyncId);
		cmd.Parameters.AddWithValue("@author", book.Author);
		cmd.Parameters.AddWithValue("@isInLibrary", book.IsInLibrary ? 1 : 0);
		cmd.Parameters.AddWithValue("@dateAdded", book.DateAdded);
		cmd.Parameters.AddWithValue("@lastOpenedDate", (object?)book.LastOpenedDate ?? DBNull.Value);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				initLock?.Dispose();
				logger.Info("Database disposed");
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}