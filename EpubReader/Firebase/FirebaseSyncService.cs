using System.Diagnostics;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.Data.Sqlite;

namespace EpubReader.Firebase;

/// <summary>
/// Firebase-based sync implementation with real-time updates and offline queue management.
/// </summary>
public partial class FirebaseSyncService : ISyncService, IDisposable
{
	const string deviceIdKey = "Sync.DeviceId";
	const string dbUrlKey = "Firebase.DatabaseUrl";
	const string localOnlyModeKey = "Sync.LocalOnlyMode";
	const string usersNode = "users";
	const string booksNode = "books";
	const string sqlInteger = "INTEGER";
	readonly string deviceId;
	readonly string deviceName;
	readonly Subject<ReadingProgress> saveSubject = new();
	readonly CompositeDisposable subscriptions = [];
	readonly IAuthentication authenticationService;
	readonly SemaphoreSlim dbInitLock = new(1, 1);
	string databaseUrl = string.Empty;
	string? userId;
	bool isLocalOnlyMode;
	FirebaseClient? firebaseClient;
	string? localDbPath;
	bool isLocalDbInitialized;
	bool disposed;

	public event EventHandler<ReadingProgress>? ProgressSynced;

	public bool IsLocalOnly => isLocalOnlyMode;

	public FirebaseSyncService(IAuthentication authentication)
	{
		this.authenticationService = authentication;
		deviceId = Preferences.Get(deviceIdKey, string.Empty);
		if (string.IsNullOrWhiteSpace(deviceId))
		{
			deviceId = $"device-{Guid.NewGuid():N}";
			Preferences.Set(deviceIdKey, deviceId);
		}
		deviceName = DeviceInfo.Name ?? "unknown";

		// Set up debounced save pipeline
		subscriptions.Add(saveSubject
			.GroupBy(p => p.BookId)
			.SelectMany(group => group
				.SelectMany(progress => Observable.FromAsync(ct => PushToCloudAsync(progress, ct))))
			.Subscribe(onNext: _ => { }));
	}

	public async Task DeleteAllCloudDataAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		EnsureUserSet();

		// Best-effort: clear local sync cache/queue so old items don't re-upload
		try
		{
			await InitializeLocalDbAsync(token);
			using var conn = await OpenLocalDbAsync(token);
			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = "DELETE FROM ReadingProgress";
				await cmd.ExecuteNonQueryAsync(token);
			}
			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = "DELETE FROM SyncQueue";
				await cmd.ExecuteNonQueryAsync(token);
			}
			Trace.TraceInformation("Local sync cache cleared");
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed clearing local sync cache: {ex.Message}");
		}

		// In local-only mode or when Firebase isn't configured, nothing to delete remotely
		if (isLocalOnlyMode || !IsConfigured() || firebaseClient is null)
		{
			Trace.TraceInformation("DeleteAllCloudData: skipped remote delete (local-only or not configured)");
			return;
		}

		try
		{
			// Remove all book progress under the user node
			await firebaseClient
				.Child(usersNode)
				.Child(userId!)
				.Child(booksNode)
				.DeleteAsync();
			Trace.TraceInformation($"Deleted all cloud data for user {userId}");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Failed deleting cloud data: {ex.Message}");
			throw;
		}
	}

	public async Task InitializeAsync(string userId, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		this.userId = userId;
		isLocalOnlyMode = false;
		try
		{
			Preferences.Set(localOnlyModeKey, false);
		}
		catch (Exception e)
		{
			Trace.TraceError($"Failed to set local-only mode preference: {e.Message}");
		}


		LoadFirebaseConfig();

		await InitializeLocalDbAsync(token).ConfigureAwait(false);

		if (!IsConfigured())
		{
			Trace.TraceWarning("Firebase not configured. Set Firebase.ApiKey and Firebase.DatabaseUrl in Preferences.");
			Trace.TraceWarning("Firebase not configured. Update FirebaseConfig.cs with your Firebase credentials.");
			return;
		}

		ConfigureFirebaseClient();
		Trace.TraceInformation($"Firebase sync initialized for user {userId} on {deviceId}");
	}

	void LoadFirebaseConfig()
	{
		try
		{
			databaseUrl = Preferences.Get(dbUrlKey, string.Empty);
			databaseUrl = FirebaseConfig.DatabaseUrl;
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Failed to load Firebase config from Preferences: {ex.Message}");
		}
	}

	void ConfigureFirebaseClient()
	{
		firebaseClient = new FirebaseClient(
			databaseUrl,
			new FirebaseOptions
			{
				AuthTokenAsyncFactory = async () => await authenticationService.GetAuthTokenAsync()
			});
	}


	public async Task InitializeLocalOnlyAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		// Generate a local user ID
		userId = $"local-{deviceId}";
		isLocalOnlyMode = true;
		try
		{
			Preferences.Set(localOnlyModeKey, true);
		}
		catch (Exception e)
		{
			Trace.TraceError($"Failed to set local-only mode preference: {e.Message}");
		}

		await InitializeLocalDbAsync(token);

		Trace.TraceInformation($"Sync service initialized in local-only mode for {userId}");
	}

	public async Task<ReadingProgress?> GetProgressAsync(string bookId, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		EnsureUserSet();

		// Try local cache first
		ReadingProgress? local = await GetLocalProgressAsync(bookId, token);

		// In local-only mode, don't attempt cloud sync
		if (isLocalOnlyMode || !IsConfigured() || firebaseClient is null)
		{
			return local;
		}

		try
		{
			ReadingProgress cloudProgress = await firebaseClient
				.Child(usersNode)
				.Child(userId!)
				.Child(booksNode)
				.Child(bookId)
				.OnceSingleAsync<ReadingProgress>();

			ReadingProgress? newest = SelectNewestProgress(local, cloudProgress);
			if (ShouldPersistToLocalCache(newest))
			{
				await SaveLocalProgressAsync(newest!, token);
			}

			return newest;
		}
		catch (Exception ex)
		{
			Trace.TraceError($"GetProgressAsync error: {ex.Message}");
			return local;
		}
	}

	public async Task<ReadingProgress?> GetCloudProgressAsync(string bookId, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		EnsureUserSet();

		if (isLocalOnlyMode || !IsConfigured() || firebaseClient is null)
		{
			return null;
		}

		try
		{
			return await firebaseClient
				   .Child(usersNode)
				   .Child(userId!)
				   .Child(booksNode)
				   .Child(bookId)
				   .OnceSingleAsync<ReadingProgress>();
		}
		catch (Exception ex)
		{
			Trace.TraceError($"GetCloudProgressAsync error: {ex.Message}");
			return null;
		}
	}

	public async Task SaveProgressAsync(ReadingProgress progress, CancellationToken token = default)
	{
		ArgumentNullException.ThrowIfNull(progress);
		token.ThrowIfCancellationRequested();
		EnsureUserSet();

		// Ensure device metadata
		progress.DeviceId = string.IsNullOrWhiteSpace(progress.DeviceId) ? deviceId : progress.DeviceId;
		progress.DeviceName = string.IsNullOrWhiteSpace(progress.DeviceName) ? deviceName : progress.DeviceName;
		progress.LastUpdated = string.IsNullOrWhiteSpace(progress.LastUpdated) ? DateTimeOffset.UtcNow.ToString("o") : progress.LastUpdated;

		// Save locally first (fast)
		await SaveLocalProgressAsync(progress, token);

		// In local-only mode, skip cloud sync
		if (isLocalOnlyMode)
		{
			Trace.TraceInformation($"Progress saved locally for {progress.BookId} (local-only mode)");
			return;
		}
		try
		{
			// Push to cloud immediately when online; otherwise queue for later delivery.
			if (await IsOnlineAsync(token))
			{
				saveSubject.OnNext(progress);
			}
			else
			{
				await QueueProgressAsync(progress, token);
			}
		}
		catch (Exception ex)
		{
			Trace.TraceError($"SaveProgressAsync error: {ex.Message}");
		}
	}

	public async Task<IDisposable> SubscribeToRemoteChangesAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		EnsureUserSet();

		// In local-only mode, don't subscribe to remote changes
		if (isLocalOnlyMode)
		{
			Trace.TraceInformation("Local-only mode: Skipping remote change subscription");
			return new CompositeDisposable();
		}

		if (!IsConfigured() || firebaseClient is null)
		{
			Trace.TraceWarning("Cannot subscribe to remote changes: Firebase not configured");
			return new CompositeDisposable();
		}

		try
		{
			IDisposable subscription = firebaseClient
				 .Child(usersNode)
				 .Child(userId!)
				 .Child(booksNode)
				 .AsObservable<ReadingProgress>()
				 .Where(change => change.EventType == global::Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
			  .Where(change => change.Object is not null)
				 .SelectMany(change => Observable.FromAsync(ct => HandleRemoteProgressChangeAsync(change.Object, change.Key, ct)))
				 .Subscribe(
					 onNext: _ => { },
					 onError: ex => Trace.TraceError($"Remote subscription error: {ex.Message}"));

			subscriptions.Add(subscription);
			Trace.TraceInformation("Subscribed to real-time Firebase updates");
			return subscription;
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Failed to subscribe to remote changes: {ex.Message}");
			return new CompositeDisposable();
		}
	}

	public async Task FlushOfflineQueueAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		// Skip when not configured or running in local-only mode
		if (isLocalOnlyMode || !IsConfigured())
		{
			return;
		}

		// Without a user id we cannot flush; just exit quietly to avoid crashing on app lifecycle events
		if (string.IsNullOrWhiteSpace(userId))
		{
			Trace.TraceWarning("Flush skipped: user not set");
			return;
		}

		if (!await IsOnlineAsync(token))
		{
			return;
		}

		List<SyncQueueItem> queued = await GetQueuedItemsAsync(token);
		foreach (SyncQueueItem item in queued)
		{
			ReadingProgress progress = new()
			{
				BookId = item.BookId,
				CurrentChapter = item.CurrentChapter,
				CurrentPage = item.CurrentPage,
				CharacterPosition = item.CharacterPosition,
				MediaOverlayEnabled = item.MediaOverlayEnabled,
				MediaOverlayChapter = item.MediaOverlayChapter,
				MediaOverlaySegmentIndex = item.MediaOverlaySegmentIndex,
				MediaOverlayPositionSeconds = item.MediaOverlayPositionSeconds,
				MediaOverlayFragmentId = item.MediaOverlayFragmentId,
				LastUpdated = item.Timestamp,
				DeviceId = deviceId,
				DeviceName = deviceName,
				IsSynced = false
			};

			try
			{
				await PushToCloudAsync(progress, token);
				await RemoveQueueItemAsync(item.Id, token);
			}
			catch (Exception ex)
			{
				Trace.TraceError($"Failed to flush queued item {item.Id}: {ex.Message}");
			}
		}
	}

	public Task<bool> IsOnlineAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		bool isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
		return Task.FromResult(isOnline);
	}

	static string LocalDbConnectionString(string dbPath) => new SqliteConnectionStringBuilder
	{
		DataSource = dbPath,
		Mode = SqliteOpenMode.ReadWriteCreate,
		Cache = SqliteCacheMode.Shared
	}.ToString();

	async Task<SqliteConnection> OpenLocalDbAsync(CancellationToken token)
	{
		await InitializeLocalDbAsync(token);
		token.ThrowIfCancellationRequested();
		var conn = new SqliteConnection(LocalDbConnectionString(localDbPath!));
		await conn.OpenAsync(token);
		return conn;
	}

	async Task InitializeLocalDbAsync(CancellationToken token)
	{
		if (isLocalDbInitialized)
		{
			return;
		}

		await dbInitLock.WaitAsync(token);
		try
		{
			if (isLocalDbInitialized)
			{
				return;
			}

			localDbPath = Path.Combine(Database.Db.DbPath, "..", "SyncCache.db");

			// Ensure the directory exists before creating the database
			string? directory = Path.GetDirectoryName(localDbPath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using var conn = new SqliteConnection(LocalDbConnectionString(localDbPath));
			await conn.OpenAsync(token);

			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = """
					CREATE TABLE IF NOT EXISTS ReadingProgress (
						BookId TEXT PRIMARY KEY NOT NULL,
						CurrentPage INTEGER NOT NULL DEFAULT 0,
						CurrentChapter INTEGER NOT NULL DEFAULT 0,
						CharacterPosition INTEGER NOT NULL DEFAULT 0,
						LastUpdated TEXT NOT NULL,
						DeviceId TEXT NOT NULL DEFAULT '',
						DeviceName TEXT NOT NULL DEFAULT '',
						IsSynced INTEGER NOT NULL DEFAULT 0,
						MediaOverlayEnabled INTEGER,
						MediaOverlayChapter INTEGER,
						MediaOverlaySegmentIndex INTEGER,
						MediaOverlayPositionSeconds REAL,
						MediaOverlayFragmentId TEXT,
						DateAdded TEXT,
						LastOpenedDate TEXT
					)
					""";
				await cmd.ExecuteNonQueryAsync(token);
			}

			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = """
					CREATE TABLE IF NOT EXISTS SyncQueue (
						Id INTEGER PRIMARY KEY AUTOINCREMENT,
						BookId TEXT NOT NULL DEFAULT '',
						CurrentPage INTEGER NOT NULL DEFAULT 0,
						CurrentChapter INTEGER NOT NULL DEFAULT 0,
						CharacterPosition INTEGER NOT NULL DEFAULT 0,
						Timestamp TEXT NOT NULL,
						RetryCount INTEGER NOT NULL DEFAULT 0,
						MediaOverlayEnabled INTEGER,
						MediaOverlayChapter INTEGER,
						MediaOverlaySegmentIndex INTEGER,
						MediaOverlayPositionSeconds REAL,
						MediaOverlayFragmentId TEXT,
						DateAdded TEXT,
						LastOpenedDate TEXT
					)
					""";
				await cmd.ExecuteNonQueryAsync(token);
			}

			await EnsureLocalDbSchemaAsync(conn, token).ConfigureAwait(false);
			isLocalDbInitialized = true;
			Trace.TraceInformation("Local sync database initialized");
		}
		finally
		{
			dbInitLock.Release();
		}
	}

	static async Task EnsureLocalDbSchemaAsync(SqliteConnection db, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		await EnsureColumnsAsync(db, "ReadingProgress", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["MediaOverlayEnabled"] = sqlInteger,
			["MediaOverlayChapter"] = sqlInteger,
			["MediaOverlaySegmentIndex"] = sqlInteger,
			["MediaOverlayPositionSeconds"] = "REAL",
			["MediaOverlayFragmentId"] = "TEXT",
			["DateAdded"] = "TEXT",
			["LastOpenedDate"] = "TEXT",
			["CharacterPosition"] = sqlInteger
		}, token).ConfigureAwait(false);

		await EnsureColumnsAsync(db, "SyncQueue", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["MediaOverlayEnabled"] = sqlInteger,
			["MediaOverlayChapter"] = sqlInteger,
			["MediaOverlaySegmentIndex"] = sqlInteger,
			["MediaOverlayPositionSeconds"] = "REAL",
			["MediaOverlayFragmentId"] = "TEXT",
			["DateAdded"] = "TEXT",
			["LastOpenedDate"] = "TEXT",
			["CharacterPosition"] = sqlInteger
		}, token).ConfigureAwait(false);
	}

	static async Task EnsureColumnsAsync(SqliteConnection db, string tableName, IReadOnlyDictionary<string, string> columns, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			using var pragmaCmd = db.CreateCommand();
#pragma warning disable S2077
			pragmaCmd.CommandText = $"PRAGMA table_info({tableName})";
#pragma warning restore S2077
			using var reader = await pragmaCmd.ExecuteReaderAsync(token);
			while (await reader.ReadAsync(token))
			{
				existingNames.Add(reader.GetString(1));
			}
		}
		catch (Exception)
		{
			// Table may not exist yet; CreateTableAsync should have created it.
			return;
		}

		foreach ((string? name, string? type) in columns)
		{
			if (existingNames.Contains(name))
			{
				continue;
			}
			// SQLite allows ADD COLUMN without NOT NULL constraint for migrations.
			using var alterCmd = db.CreateCommand();
#pragma warning disable S2077
			alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {name} {type}";
#pragma warning restore S2077
			await alterCmd.ExecuteNonQueryAsync(token);
		}
	}

	public async Task<ReadingProgress?> GetLocalProgressAsync(string bookId, CancellationToken token = default)
	{
		using var conn = await OpenLocalDbAsync(token);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT BookId, CurrentPage, CurrentChapter, CharacterPosition, LastUpdated, DeviceId, DeviceName, IsSynced, MediaOverlayEnabled, MediaOverlayChapter, MediaOverlaySegmentIndex, MediaOverlayPositionSeconds, MediaOverlayFragmentId, DateAdded, LastOpenedDate FROM ReadingProgress WHERE BookId = @bookId";
		cmd.Parameters.AddWithValue("@bookId", bookId);
		using var reader = await cmd.ExecuteReaderAsync(token);
		if (await reader.ReadAsync(token))
		{
			return ReadReadingProgress(reader);
		}
		return null;
	}

	async Task SaveLocalProgressAsync(ReadingProgress progress, CancellationToken token)
	{
		using var conn = await OpenLocalDbAsync(token);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = """
			INSERT OR REPLACE INTO ReadingProgress (BookId, CurrentPage, CurrentChapter, CharacterPosition, LastUpdated, DeviceId, DeviceName, IsSynced, MediaOverlayEnabled, MediaOverlayChapter, MediaOverlaySegmentIndex, MediaOverlayPositionSeconds, MediaOverlayFragmentId, DateAdded, LastOpenedDate)
			VALUES (@bookId, @currentPage, @currentChapter, @characterPosition, @lastUpdated, @deviceId, @deviceName, @isSynced, @mediaOverlayEnabled, @mediaOverlayChapter, @mediaOverlaySegmentIndex, @mediaOverlayPositionSeconds, @mediaOverlayFragmentId, @dateAdded, @lastOpenedDate)
			""";
		BindReadingProgressParameters(cmd, progress);
		await cmd.ExecuteNonQueryAsync(token);
	}

	async Task QueueProgressAsync(ReadingProgress progress, CancellationToken token)
	{
		using var conn = await OpenLocalDbAsync(token);

		SyncQueueItem queuedItem = new()
		{
			BookId = progress.BookId,
			CurrentChapter = progress.CurrentChapter,
			CurrentPage = progress.CurrentPage,
			CharacterPosition = progress.CharacterPosition,
			MediaOverlayEnabled = progress.MediaOverlayEnabled,
			MediaOverlayChapter = progress.MediaOverlayChapter,
			MediaOverlaySegmentIndex = progress.MediaOverlaySegmentIndex,
			MediaOverlayPositionSeconds = progress.MediaOverlayPositionSeconds,
			MediaOverlayFragmentId = progress.MediaOverlayFragmentId,
			Timestamp = progress.LastUpdated,
			RetryCount = 0
		};
		using var cmd = conn.CreateCommand();
		cmd.CommandText = """
			INSERT INTO SyncQueue (BookId, CurrentPage, CurrentChapter, CharacterPosition, Timestamp, RetryCount, MediaOverlayEnabled, MediaOverlayChapter, MediaOverlaySegmentIndex, MediaOverlayPositionSeconds, MediaOverlayFragmentId, DateAdded, LastOpenedDate)
			VALUES (@bookId, @currentPage, @currentChapter, @characterPosition, @timestamp, @retryCount, @mediaOverlayEnabled, @mediaOverlayChapter, @mediaOverlaySegmentIndex, @mediaOverlayPositionSeconds, @mediaOverlayFragmentId, @dateAdded, @lastOpenedDate)
			""";
		BindSyncQueueItemParameters(cmd, queuedItem);
		await cmd.ExecuteNonQueryAsync(token);
		Trace.TraceInformation($"Queued progress for {progress.BookId}");
	}

	async Task<List<SyncQueueItem>> GetQueuedItemsAsync(CancellationToken token)
	{
		using var conn = await OpenLocalDbAsync(token);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT Id, BookId, CurrentPage, CurrentChapter, CharacterPosition, Timestamp, RetryCount, MediaOverlayEnabled, MediaOverlayChapter, MediaOverlaySegmentIndex, MediaOverlayPositionSeconds, MediaOverlayFragmentId, DateAdded, LastOpenedDate FROM SyncQueue";
		using var reader = await cmd.ExecuteReaderAsync(token);
		var results = new List<SyncQueueItem>();
		while (await reader.ReadAsync(token))
		{
			results.Add(ReadSyncQueueItem(reader));
		}
		return results;
	}

	async Task RemoveQueueItemAsync(int id, CancellationToken token)
	{
		using var conn = await OpenLocalDbAsync(token);
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "DELETE FROM SyncQueue WHERE Id = @id";
		cmd.Parameters.AddWithValue("@id", id);
		await cmd.ExecuteNonQueryAsync(token);
	}

	async Task PushToCloudAsync(ReadingProgress progress, CancellationToken token)
	{
		// In local-only mode, don't push to cloud
		if (isLocalOnlyMode)
		{
			return;
		}

		if (!IsConfigured() || firebaseClient is null)
		{
			Trace.TraceWarning("Firebase not configured; skipping cloud sync");
			return;
		}

		try
		{
			progress.DeviceId = string.IsNullOrWhiteSpace(progress.DeviceId) ? deviceId : progress.DeviceId;
			progress.DeviceName = string.IsNullOrWhiteSpace(progress.DeviceName) ? deviceName : progress.DeviceName;
			progress.LastUpdated = string.IsNullOrWhiteSpace(progress.LastUpdated) ? DateTimeOffset.UtcNow.ToString("o") : progress.LastUpdated;

			// Fetch current cloud version to apply latest-timestamp-wins conflict resolution
			ReadingProgress? cloudProgress = null;
			try
			{
				cloudProgress = await firebaseClient
					.Child(usersNode)
					.Child(userId!)
					.Child(booksNode)
					.Child(progress.BookId)
					.OnceSingleAsync<ReadingProgress>();
			}
			catch
			{
				// Cloud entry may not exist; proceed with push
			}

			if (cloudProgress is not null && IsNewer(cloudProgress, progress))
			{
				ReadingProgress newestCloudProgress = ReconcileDateFields(cloudProgress, progress);
				if (ShouldPersistToLocalCache(newestCloudProgress))
				{
					await SaveLocalProgressAsync(newestCloudProgress, token);
				}
				ProgressSynced?.Invoke(this, newestCloudProgress);
				Trace.TraceInformation($"Skipped cloud overwrite for {progress.BookId}; remote position is newer.");
				return;
			}

			progress = cloudProgress is null
				? progress
				: ReconcileDateFields(progress, cloudProgress);

			await firebaseClient
				.Child(usersNode)
				.Child(userId!)
				.Child(booksNode)
				.Child(progress.BookId)
				.PutAsync(progress);

			progress.IsSynced = true;
			await SaveLocalProgressAsync(progress, token);
			ProgressSynced?.Invoke(this, progress);
			Trace.TraceInformation($"Progress synced for {progress.BookId} from {deviceId}");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Cloud sync failed for {progress.BookId}: {ex.Message}");
			await QueueProgressAsync(progress, token);
		}
	}

	async Task HandleRemoteProgressChangeAsync(ReadingProgress progress, string? key, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(progress);

		ReadingProgress? localProgress = await GetLocalProgressAsync(progress.BookId, token);
		ReadingProgress? newest = SelectNewestProgress(localProgress, progress);
		if (ShouldPersistToLocalCache(newest))
		{
			await SaveLocalProgressAsync(newest!, token);
		}

		if (!string.Equals(progress.DeviceId, deviceId, StringComparison.Ordinal))
		{
			Trace.TraceInformation($"Remote change detected for {key ?? progress.BookId} from {progress.DeviceId}");
			ProgressSynced?.Invoke(this, newest ?? progress);
		}
	}

	static ReadingProgress ReconcileDateFields(ReadingProgress local, ReadingProgress cloud)
	{
		// Latest-timestamp-wins conflict resolution for DateAdded and LastOpenedDate
		// Keep the value with the more recent timestamp, or local if timestamps are equal

		local.DateAdded = GetNewerDateString(cloud.DateAdded, local.DateAdded);
		local.LastOpenedDate = GetNewerDateString(cloud.LastOpenedDate, local.LastOpenedDate);

		return local;
	}

	static string? GetNewerDateString(string? first, string? second)
	{
		if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(second))
		{
			return second;
		}

		if (string.IsNullOrWhiteSpace(first))
		{
			return second;
		}

		if (string.IsNullOrWhiteSpace(second))
		{
			return first;
		}

		if (DateTimeOffset.TryParse(first, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset firstTime) &&
			DateTimeOffset.TryParse(second, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset secondTime))
		{
			return firstTime > secondTime ? first : second;
		}

		return second;
	}

	static bool IsNewer(ReadingProgress first, ReadingProgress second)
	{
		return ParseTimestamp(first.LastUpdated) > ParseTimestamp(second.LastUpdated);
	}

	static ReadingProgress? SelectNewestProgress(ReadingProgress? local, ReadingProgress? cloud)
	{
		if (local is null)
		{
			return cloud;
		}

		if (cloud is null)
		{
			return local;
		}

#pragma warning disable S2234
		return IsNewer(cloud, local)
			? ReconcileDateFields(cloud, local)
			: ReconcileDateFields(local, cloud);
#pragma warning restore S2234
	}

	static DateTimeOffset ParseTimestamp(string? timestamp)
	{
		return DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
			? parsed
			: DateTimeOffset.MinValue;
	}

	static bool ShouldPersistToLocalCache(ReadingProgress? progress)
	{
		// The local cache mirrors the best known progress for this book, regardless of
		// which device produced it — it must be refreshed even when the newest position
		// came from another device.
		return progress is not null;
	}

	void EnsureUserSet()
	{
		if (string.IsNullOrWhiteSpace(userId))
		{
			throw new InvalidOperationException("Sync service not initialized with a user id.");
		}
	}

	bool IsConfigured() => !string.IsNullOrWhiteSpace(databaseUrl);

	// ── Mapping helpers for sync local database ──

	static ReadingProgress ReadReadingProgress(SqliteDataReader reader)
	{
		return new ReadingProgress
		{
			BookId = reader.GetString(0),
			CurrentPage = reader.GetInt32(1),
			CurrentChapter = reader.GetInt32(2),
			CharacterPosition = reader.GetInt32(3),
			LastUpdated = reader.GetString(4),
			DeviceId = reader.GetString(5),
			DeviceName = reader.GetString(6),
			IsSynced = reader.GetInt32(7) != 0,
			MediaOverlayEnabled = reader.IsDBNull(8) ? null : reader.GetBoolean(8),
			MediaOverlayChapter = reader.IsDBNull(9) ? null : reader.GetInt32(9),
			MediaOverlaySegmentIndex = reader.IsDBNull(10) ? null : reader.GetInt32(10),
			MediaOverlayPositionSeconds = reader.IsDBNull(11) ? null : reader.GetDouble(11),
			MediaOverlayFragmentId = reader.IsDBNull(12) ? null : reader.GetString(12),
			DateAdded = reader.IsDBNull(13) ? null : reader.GetString(13),
			LastOpenedDate = reader.IsDBNull(14) ? null : reader.GetString(14)
		};
	}

	static SyncQueueItem ReadSyncQueueItem(SqliteDataReader reader)
	{
		return new SyncQueueItem
		{
			Id = reader.GetInt32(0),
			BookId = reader.GetString(1),
			CurrentPage = reader.GetInt32(2),
			CurrentChapter = reader.GetInt32(3),
			CharacterPosition = reader.GetInt32(4),
			Timestamp = reader.GetString(5),
			RetryCount = reader.GetInt32(6),
			MediaOverlayEnabled = reader.IsDBNull(7) ? null : reader.GetBoolean(7),
			MediaOverlayChapter = reader.IsDBNull(8) ? null : reader.GetInt32(8),
			MediaOverlaySegmentIndex = reader.IsDBNull(9) ? null : reader.GetInt32(9),
			MediaOverlayPositionSeconds = reader.IsDBNull(10) ? null : reader.GetDouble(10),
			MediaOverlayFragmentId = reader.IsDBNull(11) ? null : reader.GetString(11),
			DateAdded = reader.IsDBNull(12) ? null : reader.GetString(12),
			LastOpenedDate = reader.IsDBNull(13) ? null : reader.GetString(13)
		};
	}

	static void BindReadingProgressParameters(SqliteCommand cmd, ReadingProgress progress)
	{
		cmd.Parameters.AddWithValue("@bookId", progress.BookId);
		cmd.Parameters.AddWithValue("@currentPage", progress.CurrentPage);
		cmd.Parameters.AddWithValue("@currentChapter", progress.CurrentChapter);
		cmd.Parameters.AddWithValue("@characterPosition", progress.CharacterPosition);
		cmd.Parameters.AddWithValue("@lastUpdated", progress.LastUpdated);
		cmd.Parameters.AddWithValue("@deviceId", progress.DeviceId);
		cmd.Parameters.AddWithValue("@deviceName", progress.DeviceName);
		cmd.Parameters.AddWithValue("@isSynced", progress.IsSynced ? 1 : 0);
		cmd.Parameters.AddWithValue("@mediaOverlayEnabled", (object?)progress.MediaOverlayEnabled ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlayChapter", (object?)progress.MediaOverlayChapter ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlaySegmentIndex", (object?)progress.MediaOverlaySegmentIndex ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlayPositionSeconds", (object?)progress.MediaOverlayPositionSeconds ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlayFragmentId", (object?)progress.MediaOverlayFragmentId ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@dateAdded", (object?)progress.DateAdded ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@lastOpenedDate", (object?)progress.LastOpenedDate ?? DBNull.Value);
	}

	static void BindSyncQueueItemParameters(SqliteCommand cmd, SyncQueueItem item)
	{
		cmd.Parameters.AddWithValue("@bookId", item.BookId);
		cmd.Parameters.AddWithValue("@currentPage", item.CurrentPage);
		cmd.Parameters.AddWithValue("@currentChapter", item.CurrentChapter);
		cmd.Parameters.AddWithValue("@characterPosition", item.CharacterPosition);
		cmd.Parameters.AddWithValue("@timestamp", item.Timestamp);
		cmd.Parameters.AddWithValue("@retryCount", item.RetryCount);
		cmd.Parameters.AddWithValue("@mediaOverlayEnabled", (object?)item.MediaOverlayEnabled ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlayChapter", (object?)item.MediaOverlayChapter ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlaySegmentIndex", (object?)item.MediaOverlaySegmentIndex ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlayPositionSeconds", (object?)item.MediaOverlayPositionSeconds ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@mediaOverlayFragmentId", (object?)item.MediaOverlayFragmentId ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@dateAdded", (object?)item.DateAdded ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@lastOpenedDate", (object?)item.LastOpenedDate ?? DBNull.Value);
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposed)
		{
			return;
		}
		if (disposing)
		{
			subscriptions.Dispose();
			saveSubject.Dispose();
		}
		disposed = true;
	}
}