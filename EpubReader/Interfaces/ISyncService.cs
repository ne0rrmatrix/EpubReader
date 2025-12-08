using EpubReader.Models;

namespace EpubReader.Interfaces;

/// <summary>
/// Defines cloud sync operations for reading progress with real-time synchronization support.
/// </summary>
public interface ISyncService
{
	/// <summary>
	/// Raised when reading progress has been successfully synced to the cloud.
	/// </summary>
	event EventHandler<ReadingProgress>? ProgressSynced;

	/// <summary>
	/// Initializes the sync service with a user identifier for cloud sync.
	/// </summary>
	/// <param name="userId">The unique identifier for the user.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task InitializeAsync(string userId, CancellationToken token = default);

	/// <summary>
	/// Initializes the sync service in local-only mode (no cloud sync).
	/// </summary>
	/// <param name="token">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task InitializeLocalOnlyAsync(CancellationToken token = default);

	/// <summary>
	/// Retrieves reading progress for a specific book from local cache or cloud.
	/// </summary>
	/// <param name="bookId">The unique identifier for the book.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>The reading progress, or null if not found.</returns>
	Task<ReadingProgress?> GetProgressAsync(string bookId, CancellationToken token = default);

	/// <summary>
	/// Retrieves reading progress from the cloud only, bypassing local cache.
	/// </summary>
	/// <param name="bookId">The unique identifier for the book.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>The cloud reading progress, or null if not found.</returns>
	Task<ReadingProgress?> GetCloudProgressAsync(string bookId, CancellationToken token = default);

	/// <summary>
	/// Retrieves reading progress from the local cache only.
	/// </summary>
	/// <param name="bookId">The unique identifier for the book.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>The local reading progress, or null if not found.</returns>
	Task<ReadingProgress?> GetLocalProgressAsync(string bookId, CancellationToken token = default);

	/// <summary>
	/// Saves reading progress locally and syncs to cloud when online.
	/// Updates are debounced to prevent excessive sync operations.
	/// </summary>
	/// <param name="progress">The reading progress to save.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task SaveProgressAsync(ReadingProgress progress, CancellationToken token = default);

	/// <summary>
	/// Subscribes to real-time changes from the cloud for all books.
	/// Changes will trigger the ProgressSynced event.
	/// </summary>
	/// <param name="token">Cancellation token to stop the subscription.</param>
	/// <returns>A disposable subscription that should be disposed when no longer needed.</returns>
	Task<IDisposable> SubscribeToRemoteChangesAsync(CancellationToken token = default);

	/// <summary>
	/// Flushes any pending offline changes to the cloud.
	/// Called during app lifecycle events like OnSleep.
	/// </summary>
	/// <param name="token">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task FlushOfflineQueueAsync(CancellationToken token = default);

	/// <summary>
	/// Checks if the device has internet connectivity.
	/// </summary>
	/// <param name="token">Cancellation token.</param>
	/// <returns>True if online, false otherwise.</returns>
	Task<bool> IsOnlineAsync(CancellationToken token = default);

	/// <summary>
	/// Indicates whether the sync service is running in local-only mode.
	/// </summary>
	bool IsLocalOnly { get; }
}