using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EpubReader.Models;

namespace EpubReader.Util;

/// <summary>
/// Provides a deterministic sync identifier for books so progress can be shared across devices.
/// </summary>
public static class BookIdentityService
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookIdentityService));

	public static async Task<string> ComputeSyncIdAsync(Book book, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(book);
		token.ThrowIfCancellationRequested();

		if (!string.IsNullOrWhiteSpace(book.SyncId))
		{
			return book.SyncId;
		}

		if (!string.IsNullOrWhiteSpace(book.FilePath) && File.Exists(book.FilePath))
		{
			try
			{
				var fileHash = await ComputeFileHashAsync(book.FilePath, token).ConfigureAwait(false);
				var fileName = Path.GetFileName(book.FilePath);
				book.SyncId = $"file-{ComputeTextHash($"{fileName}|{fileHash}")}";
				return book.SyncId;
			}
			catch (Exception ex)
			{
				logger.Warn($"Failed to hash file for sync id: {ex.Message}");
			}
		}

		var fileNameOnly = Path.GetFileName(book.FilePath);
		var metadata = $"{fileNameOnly}|{book.Title}|{book.Author}|{book.PublishedDate?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty}|{book.Isbn}|{book.Language}";
		book.SyncId = $"meta-{ComputeTextHash(metadata)}";
		return book.SyncId;
	}

	static async Task<string> ComputeFileHashAsync(string path, CancellationToken token)
	{
		await using var stream = File.OpenRead(path);
		using var sha = SHA256.Create();
		var hash = await sha.ComputeHashAsync(stream, token).ConfigureAwait(false);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	static string ComputeTextHash(string text)
	{
		using var sha = SHA256.Create();
		var bytes = Encoding.UTF8.GetBytes(text?.ToLowerInvariant() ?? string.Empty);
		var hash = sha.ComputeHash(bytes);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}
}
