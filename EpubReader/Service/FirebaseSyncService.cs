using System.Diagnostics;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Firebase.Database;
using Firebase.Database.Query;
using SQLite;

namespace EpubReader.Service;

/// <summary>
/// Firebase-based sync implementation with real-time updates and offline queue management.
/// </summary>
public sealed class FirebaseSyncService : ISyncService, IDisposable
{
	const string deviceIdKey = "Sync.DeviceId";
	const string dbUrlKey = "Firebase.DatabaseUrl";
	const string localOnlyModeKey = "Sync.LocalOnlyMode";
	const string usersNode = "users";
	const string booksNode = "books";
	const string sqlInteger = "INTEGER";
	readonly SQLite.SQLiteOpenFlags flags = SQLite.SQLiteOpenFlags.ReadWrite | SQLite.SQLiteOpenFlags.Create | SQLite.SQLiteOpenFlags.SharedCache;
	readonly string deviceId;
	readonly string deviceName;
	readonly Subject<ReadingProgress> saveSubject = new();
	readonly CompositeDisposable subscriptions = [];
	readonly AuthenticationService authenticationService;
	string databaseUrl = string.Empty;
	string? userId;
	bool isLocalOnlyMode;
	FirebaseClient? firebaseClient;
	SQLiteAsyncConnection? localDb;
	bool disposed;

	public event EventHandler<ReadingProgress>? ProgressSynced;

	public bool IsLocalOnly => isLocalOnlyMode;

	public FirebaseSyncService(AuthenticationService authenticationService)
	{
		this.authenticationService = authenticationService;
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
			if (localDb is null)
			{
				await InitializeLocalDbAsync(token);
			}
			await localDb!.DeleteAllAsync<ReadingProgress>().WaitAsync(token);
			await localDb!.DeleteAllAsync<SyncQueueItem>().WaitAsync(token);
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
		var local = await GetLocalProgressAsync(bookId, token);

		// In local-only mode, don't attempt cloud sync
		if (isLocalOnlyMode || !IsConfigured() || firebaseClient is null)
		{
			return local;
		}

		try
		{
			var cloudProgress = await firebaseClient
				.Child(usersNode)
				.Child(userId!)
				.Child(booksNode)
				.Child(bookId)
				.OnceSingleAsync<ReadingProgress>();

			// Return the newest version
			if (cloudProgress is not null && local is not null)
			{
				return IsNewer(cloudProgress, local) ? cloudProgress : local;
			}

			return cloudProgress ?? local;
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
			// Queue for cloud sync (debounced)
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
			var subscription = firebaseClient
				.Child(usersNode)
				.Child(userId!)
				.Child(booksNode)
				.AsObservable<ReadingProgress>()
				.Where(change => change.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
				.Subscribe(
					onNext: change =>
					{
						if (change.Object is not null && change.Object.DeviceId != deviceId)
						{
							Trace.TraceInformation($"Remote change detected for {change.Key} from {change.Object.DeviceId}");
							ProgressSynced?.Invoke(this, change.Object);
						}
					},
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

		var queued = await GetQueuedItemsAsync(token);
		foreach (var item in queued)
		{
			var progress = new ReadingProgress
			{
				BookId = item.BookId,
				CurrentChapter = item.CurrentChapter,
				CurrentPage = item.CurrentPage,
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
		var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
		return Task.FromResult(isOnline);
	}

	async Task InitializeLocalDbAsync(CancellationToken token)
	{
		if (localDb is not null)
		{
			return;
		}

		var dbPath = Path.Combine(Database.Db.DbPath, "..", "SyncCache.db");
		localDb = new SQLiteAsyncConnection(dbPath, flags);
		await localDb.CreateTableAsync<ReadingProgress>().WaitAsync(token);
		await localDb.CreateTableAsync<SyncQueueItem>().WaitAsync(token);
		await EnsureLocalDbSchemaAsync(localDb, token).ConfigureAwait(false);
		Trace.TraceInformation("Local sync database initialized");
	}

	static async Task EnsureLocalDbSchemaAsync(SQLiteAsyncConnection db, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		await EnsureColumnsAsync(db, "ReadingProgress", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["MediaOverlayEnabled"] = sqlInteger,
			["MediaOverlayChapter"] = sqlInteger,
			["MediaOverlaySegmentIndex"] = sqlInteger,
			["MediaOverlayPositionSeconds"] = "REAL",
			["MediaOverlayFragmentId"] = "TEXT"
		}, token).ConfigureAwait(false);

		await EnsureColumnsAsync(db, "SyncQueue", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["MediaOverlayEnabled"] = sqlInteger,
			["MediaOverlayChapter"] = sqlInteger,
			["MediaOverlaySegmentIndex"] = sqlInteger,
			["MediaOverlayPositionSeconds"] = "REAL",
			["MediaOverlayFragmentId"] = "TEXT"
		}, token).ConfigureAwait(false);
	}

	static async Task EnsureColumnsAsync(SQLiteAsyncConnection db, string tableName, IReadOnlyDictionary<string, string> columns, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		List<SQLiteConnection.ColumnInfo> tableInfo;
		try
		{
			tableInfo = await db.GetTableInfoAsync(tableName).WaitAsync(token);
		}
		catch (Exception)
		{
			// Table may not exist yet; CreateTableAsync should have created it.
			return;
		}

		var existing = new HashSet<string>(tableInfo.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
		foreach (var (name, type) in columns)
		{
			if (existing.Contains(name))
			{
				continue;
			}
			// SQLite allows ADD COLUMN without NOT NULL constraint for migrations.
			var sql = $"ALTER TABLE {tableName} ADD COLUMN {name} {type}";
			await db.ExecuteAsync(sql).WaitAsync(token);
		}
	}

	public async Task<ReadingProgress?> GetLocalProgressAsync(string bookId, CancellationToken token = default)
	{
		if (localDb is null)
		{
			await InitializeLocalDbAsync(token);
		}
		return await localDb!.Table<ReadingProgress>().FirstOrDefaultAsync(x => x.BookId == bookId).WaitAsync(token);
	}

	async Task SaveLocalProgressAsync(ReadingProgress progress, CancellationToken token)
	{
		if (localDb is null)
		{
			await InitializeLocalDbAsync(token);
		}
		await localDb!.InsertOrReplaceAsync(progress).WaitAsync(token);
	}

	async Task QueueProgressAsync(ReadingProgress progress, CancellationToken token)
	{
		if (localDb is null)
		{
			await InitializeLocalDbAsync(token);
		}

		var queuedItem = new SyncQueueItem
		{
			BookId = progress.BookId,
			CurrentChapter = progress.CurrentChapter,
			CurrentPage = progress.CurrentPage,
			MediaOverlayEnabled = progress.MediaOverlayEnabled,
			MediaOverlayChapter = progress.MediaOverlayChapter,
			MediaOverlaySegmentIndex = progress.MediaOverlaySegmentIndex,
			MediaOverlayPositionSeconds = progress.MediaOverlayPositionSeconds,
			MediaOverlayFragmentId = progress.MediaOverlayFragmentId,
			Timestamp = progress.LastUpdated,
			RetryCount = 0
		};
		await localDb!.InsertAsync(queuedItem).WaitAsync(token);
		Trace.TraceInformation($"Queued progress for {progress.BookId}");
	}

	async Task<List<SyncQueueItem>> GetQueuedItemsAsync(CancellationToken token)
	{
		if (localDb is null)
		{
			await InitializeLocalDbAsync(token);
		}
		return await localDb!.Table<SyncQueueItem>().ToListAsync().WaitAsync(token) ?? [];
	}

	async Task RemoveQueueItemAsync(int id, CancellationToken token)
	{
		if (localDb is null)
		{
			return;
		}
		var item = await localDb.FindAsync<SyncQueueItem>(id).WaitAsync(token);
		if (item is not null)
		{
			await localDb.DeleteAsync(item).WaitAsync(token);
		}
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

	static bool IsNewer(ReadingProgress first, ReadingProgress second)
	{
		if (DateTimeOffset.TryParse(first.LastUpdated, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var firstTime) &&
			DateTimeOffset.TryParse(second.LastUpdated, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var secondTime))
		{
			return firstTime > secondTime;
		}
		return false;
	}

	void EnsureUserSet()
	{
		if (string.IsNullOrWhiteSpace(userId))
		{
			throw new InvalidOperationException("Sync service not initialized with a user id.");
		}
	}

	bool IsConfigured() => !string.IsNullOrWhiteSpace(databaseUrl);

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		subscriptions.Dispose();
		saveSubject.Dispose();
		GC.SuppressFinalize(this);
	}
}