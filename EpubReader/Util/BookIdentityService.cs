using System.Diagnostics;
using System.Security.Cryptography;

namespace EpubReader.Util;

/// <summary>
/// Computes a deterministic identifier for a book from the SHA-1 hash of its EPUB file content.
/// Because the hash is derived from file bytes rather than the file name, it stays stable across
/// devices even when the same book ends up stored under a different file name or path (file names
/// are not consistent across platforms), which makes it suitable both for detecting duplicate
/// imports and for matching reading progress across devices.
/// </summary>
public static class BookIdentityService
{
	public const string SyncIdPrefix = "sha1-";

	/// <summary>
	/// Computes (and assigns to <see cref="Book.SyncId"/>) the identifier for the book found at
	/// <see cref="Book.FilePath"/> by hashing the file's contents.
	/// </summary>
	public static async Task<string> ComputeSyncIdAsync(Book book, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(book);
		token.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(book.FilePath) || !File.Exists(book.FilePath))
		{
			throw new FileNotFoundException($"Cannot compute sync id: book file not found at '{book.FilePath}'.", book.FilePath);
		}

		try
		{
			await using FileStream stream = File.OpenRead(book.FilePath);
			return await ComputeSyncIdFromStreamAsync(book, stream, token).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new InvalidOperationException($"Failed to compute sync id for book '{book.Title}': {ex.Message}", ex);
		}
	}

	/// <summary>
	/// Computes (and assigns to <see cref="Book.SyncId"/>) the identifier for the book by hashing the
	/// contents of an already-open stream. Use this when the book's on-disk copy hasn't been created
	/// yet (e.g. hashing a picked file's stream before it's copied into library storage), or when
	/// <see cref="Book.FilePath"/> isn't a directly readable local path on the current platform.
	/// </summary>
	/// <remarks>The stream is rewound to the start before and after hashing when seekable, so the
	/// caller can continue reading it afterwards.</remarks>
	public static async Task<string> ComputeSyncIdFromStreamAsync(Book book, Stream stream, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(book);
		ArgumentNullException.ThrowIfNull(stream);
		token.ThrowIfCancellationRequested();

		if (stream.CanSeek)
		{
			stream.Position = 0;
		}

		// SHA-1 is used purely as a content fingerprint for dedup/sync matching, not for security.
#pragma warning disable CA5350, S4790
		byte[] hash = await SHA1.HashDataAsync(stream, token).ConfigureAwait(false);
#pragma warning restore CA5350, S4790

		if (stream.CanSeek)
		{
			stream.Position = 0;
		}

		book.SyncId = $"{SyncIdPrefix}{Convert.ToHexString(hash).ToLowerInvariant()}";
		Trace.TraceInformation($"[BookIdentity] SyncId={book.SyncId} for '{book.Title}' (file={book.FilePath})");
		return book.SyncId;
	}
}
