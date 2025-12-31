using Android.App;
using Android.Content;
using AndroidX.DocumentFile.Provider;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;
using Uri = Android.Net.Uri;

namespace EpubReader.Service;

/// <summary>
/// Provides functionality to pick a folder from the device's storage and perform operations on EPUB files within the
/// selected folder.
/// </summary>
/// <remarks>The <see cref="FolderPicker"/> class allows users to select a folder and perform asynchronous
/// operations on EPUB files found within that folder. It includes methods to enumerate EPUB files, perform file
/// operations, and handle folder selection results. This class is designed to be used in applications that require
/// folder access and file manipulation capabilities.</remarks>
public partial class FolderPicker : IFolderPicker
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FolderPicker));
	public const int PickFolderRequestCode = 1001;
	TaskCompletionSource<string>? folderPickedTcs;

	/// <summary>
	/// Asynchronously enumerates all EPUB files in the specified folder.
	/// </summary>
	/// <remarks>This method logs information if the folder URI is not provided or if an error occurs during file
	/// enumeration.</remarks>
	/// <param name="folderUri">The URI of the folder to search for EPUB files. Can be null or empty, in which case an empty list is returned.</param>
	/// <returns>A task representing the asynchronous operation. The task result contains a list of file paths to EPUB files found
	/// in the specified folder. Returns an empty list if no EPUB files are found or if the folder URI is null or empty.</returns>
	public Task<List<string>> EnumerateEpubFilesInFolderAsync(string? folderUri, CancellationToken cancellationToken = default)
	{
		List<string> epubFiles = [];

		if (string.IsNullOrEmpty(folderUri))
		{
			logger.Info("No folder URI provided.");
			return Task.FromResult(epubFiles);
		}

		try
		{
			var documentFile = GetDocumentFileFromUri(folderUri);
			if (documentFile is null)
			{
				return Task.FromResult(epubFiles);
			}

			epubFiles = GetEpubFilesFromDirectory(documentFile);
		}
		catch (Exception ex)
		{
			logger.Info($"Error enumerating files: {ex.Message}");
		}

		return Task.FromResult(epubFiles);
	}

	/// <summary>
	/// Asynchronously performs a file operation on the specified EPUB file and returns a stream for further processing.
	/// </summary>
	/// <remarks>This method logs information if the provided file path is empty or if an error occurs during the
	/// operation.</remarks>
	/// <param name="epubFilePath">The file path of the EPUB file to operate on. Cannot be null or empty.</param>
	/// <returns>A <see cref="Stream"/> representing the EPUB file content. Returns <see cref="Stream.Null"/> if the file path is
	/// empty or an error occurs during the operation.</returns>
	public async Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(epubFilePath))
		{
			logger.Info("Empty EPUB file path provided.");
			return Stream.Null;
		}

		try
		{
			return await OpenEpubStreamAsync(epubFilePath).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.Info($"Error performing operation on {epubFilePath}: {ex.Message}");
			return Stream.Null;
		}
	}

	/// <summary>
	/// Asynchronously prompts the user to select a folder and returns the folder's URI as a string.
	/// </summary>
	/// <remarks>This method launches an activity that allows the user to pick a folder from the device's storage.
	/// The method returns a task that completes when the user has selected a folder, providing the URI of the selected
	/// folder as a string. The URI can be used to access the folder's contents.</remarks>
	/// <returns>A task that represents the asynchronous operation. The task result contains the URI of the selected folder as a
	/// string.</returns>
	public async Task<string> PickFolderAsync()
	{
		folderPickedTcs = new TaskCompletionSource<string>();

		Intent intent = new(Intent.ActionOpenDocumentTree);
		intent.AddFlags(ActivityFlags.GrantReadUriPermission); // Grant permission to read files
		intent.AddFlags(ActivityFlags.GrantPersistableUriPermission); // Persist permission across device reboots

		// Start the activity for result
		(Platform.CurrentActivity as MainActivity)?.StartActivityForResult(intent, PickFolderRequestCode);

		return await folderPickedTcs.Task;
	}

	/// <summary>
	/// Handles the result of an activity that was started for picking a folder.
	/// </summary>
	/// <remarks>This method processes the result of a folder-picking activity. If the folder selection is
	/// successful, it persists URI access permissions and sets the result with the selected folder's URI. If the selection
	/// is canceled or fails, it sets an empty result.</remarks>
	/// <param name="requestCode">The integer request code originally supplied to startActivityForResult(), allowing you to identify who this result
	/// came from.</param>
	/// <param name="resultCode">The integer result code returned by the child activity through its setResult().</param>
	/// <param name="data">An Intent, which can return result data to the caller (various data can be attached to Intent "extras").</param>
	/// <exception cref="InvalidOperationException">Thrown if the content resolver is null when attempting to take persistable URI permissions.</exception>
	public void OnActivityResult(int requestCode, Result resultCode, Intent? data)
	{
		if (requestCode == PickFolderRequestCode)
		{
			if (resultCode == Result.Ok && data is not null && data.Data is not null)
			{
				Uri treeUri = data.Data;
				// Persist the URI access permissions
				var contentResolver = GetContentResolver() ?? throw new InvalidOperationException("ContentResolver is null.");
				contentResolver.TakePersistableUriPermission(treeUri, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
				var treeTempUri = treeUri.ToString();
				if (string.IsNullOrEmpty(treeTempUri))
				{
					logger.Info("Picked folder URI is empty.");
					folderPickedTcs?.SetResult(string.Empty);
					return;
				}
				folderPickedTcs?.SetResult(treeTempUri);
			}
			else
			{
				folderPickedTcs?.SetResult(string.Empty);
				logger.Info("Folder picking was cancelled or failed.");
			}
		}
	}

	static async Task<Stream> OpenEpubStreamAsync(string epubFilePath)
	{
		Uri? epubUri = Uri.Parse(epubFilePath);
		if (epubUri is null)
		{
			logger.Info("Invalid EPUB file path.");
			return Stream.Null;
		}

		var contentResolver = GetContentResolver();
		if (contentResolver is null)
		{
			return Stream.Null;
		}

		using var inputStream = contentResolver.OpenInputStream(epubUri);
		if (inputStream is null)
		{
			logger.Info("Could not open input stream for EPUB file.");
			return Stream.Null;
		}

		var stream = new MemoryStream();
		await inputStream.CopyToAsync(stream).ConfigureAwait(false);
		stream.Position = 0; // Reset position to the beginning of the stream
		return stream;
	}

	static ContentResolver? GetContentResolver()
	{
		if (Platform.CurrentActivity?.ApplicationContext is null)
		{
			logger.Info("Current activity or application context is null.");
			return null;
		}

		return Platform.CurrentActivity.ApplicationContext.ContentResolver;
	}

	static List<string> GetEpubFilesFromDirectory(DocumentFile documentFile)
	{
		List<string> epubFiles = [];

		if (!documentFile.IsDirectory)
		{
			return epubFiles;
		}

		var listFiles = documentFile.ListFiles();
		if (listFiles is null)
		{
			logger.Info("No files found in the folder.");
			return epubFiles;
		}

		foreach (var file in listFiles.Where(x => IsEpubFile(x)))
		{
			var tempUri = file.Uri?.ToString();
			if (tempUri is null)
			{
				logger.Info("File URI is null.");
				continue;
			}
			epubFiles.Add(tempUri);
		}

		return epubFiles;
	}

	static bool IsEpubFile(DocumentFile file)
	{
		return file.IsFile &&
			   file.Name is not null &&
			   file.Name.EndsWith(".epub", StringComparison.OrdinalIgnoreCase);
	}

	static DocumentFile? GetDocumentFileFromUri(string folderUri)
	{
		try
		{
			Uri uri = Uri.Parse(folderUri) ?? throw new InvalidOperationException("Invalid folder URI");
			ArgumentNullException.ThrowIfNull(Platform.CurrentActivity);

			var documentFile = DocumentFile.FromTreeUri(Platform.CurrentActivity.ApplicationContext, uri);
			if (documentFile is null)
			{
				logger.Info("DocumentFile is null. Check if the URI is valid and permissions are granted.");
			}

			return documentFile;
		}
		catch (Exception ex)
		{
			logger.Info($"Error creating DocumentFile: {ex.Message}");
			return null;
		}
	}
}