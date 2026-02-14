using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace EpubReader.Util;

/// <summary>
/// Provides a deterministic sync identifier for books so progress can be shared across devices.
/// </summary>
public static class BookIdentityService
{
	public static async Task<string> ComputeSyncIdAsync(Book book, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(book);
		token.ThrowIfCancellationRequested();

		if (!string.IsNullOrWhiteSpace(book.SyncId))
		{
			return book.SyncId;
		}

		string fileHash;
		try
		{
			if (!string.IsNullOrWhiteSpace(book.FilePath) && File.Exists(book.FilePath))
			{
				fileHash = await ComputeFileHashAsync(book.FilePath, token).ConfigureAwait(false);
				var fileName = Path.GetFileName(book.FilePath);
				book.SyncId = $"file-{ComputeTextHash($"{fileName}|{fileHash}")}";
				return book.SyncId;
			}
			else
			{
				throw new FileNotFoundException("Book file not found", book.FilePath);
			}
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to compute file hash for book '{book.Title}': {ex.Message}", ex);
		}
	}

	static async Task<string> ComputeFileHashAsync(string path, CancellationToken token)
	{
		await using var stream = File.OpenRead(path);
		var hash = await SHA256.HashDataAsync(stream, token).ConfigureAwait(false);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	static string ComputeTextHash(string text)
	{
		var bytes = Encoding.UTF8.GetBytes(text?.ToLowerInvariant() ?? string.Empty);
		var hash = SHA256.HashData(bytes);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}
}