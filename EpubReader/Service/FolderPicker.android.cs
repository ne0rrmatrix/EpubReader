using Android.App;
using Android.Content;
using AndroidX.DocumentFile.Provider;
using EpubReader.Interfaces;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;
using Uri = Android.Net.Uri;

namespace EpubReader.Service;

public partial class FolderPicker : IFolderPicker
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FolderPicker));
	public const int PickFolderRequestCode = 1001;
	TaskCompletionSource<string>? folderPickedTcs;

	public Task<List<string>> EnumerateEpubFilesInFolderAsync(string? folderUri)
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

	public async Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath)
	{
		if (string.IsNullOrEmpty(epubFilePath))
		{
			logger.Info("Empty EPUB file path provided.");
			return Stream.Null;
		}

		try
		{
			return await OpenEpubStreamAsync(epubFilePath);
		}
		catch (Exception ex)
		{
			logger.Info($"Error performing operation on {epubFilePath}: {ex.Message}");
			return Stream.Null;
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
		await inputStream.CopyToAsync(stream);
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

	// This method will be called from MainActivity's OnActivityResult
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
}

