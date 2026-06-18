using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace EpubReader.Util;

/// <summary>
/// Provides a deterministic sync identifier for books so progress can be shared across devices.
/// </summary>
public static class BookIdentityService
{
	public static Task<string> ComputeSyncIdAsync(Book book, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(book);
		token.ThrowIfCancellationRequested();

		try
		{
			string title = (book.Title ?? string.Empty).Trim();
			string author = (book.Author ?? string.Empty).Trim();

			if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(author))
			{
				if (string.IsNullOrWhiteSpace(book.FilePath))
				{
					throw new FileNotFoundException("Book file path is null or empty, and title/author metadata is also missing.");
				}

				// Fallback: use filename when EPUB metadata is missing.
				string fileName = Path.GetFileName(book.FilePath);
				book.SyncId = $"file-{ComputeTextHash(fileName)}";
			}
			else
			{
				// Use title + author for cross-device consistency. Filenames differ across platforms.
				string identity = $"{title}|{author}";
				book.SyncId = $"book-{ComputeTextHash(identity)}";
			}

			Trace.WriteLine($"[BookIdentity] SyncId={book.SyncId} for '{book.Title}' by '{book.Author}' (file={book.FilePath})");
			return Task.FromResult(book.SyncId);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to compute sync id for book '{book.Title}': {ex.Message}", ex);
		}
	}

	static string ComputeTextHash(string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text?.ToLowerInvariant() ?? string.Empty);
		byte[] hash = SHA256.HashData(bytes);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}
}