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
	public Task<List<string>> EnumerateEpubFilesInFolderAsync(string? folderUriString)
	{
		List<string> epubFiles = [];
		if (string.IsNullOrEmpty(folderUriString))
		{
			logger.Info("No folder URI provided.");
			return Task.FromResult(epubFiles);
		}

		try
		{
			Uri folderUri = Uri.Parse(folderUriString) ?? throw new InvalidOperationException("Invalid folder URI");
			ArgumentNullException.ThrowIfNull(Platform.CurrentActivity);
			var documentFile = DocumentFile.FromTreeUri(Platform.CurrentActivity.ApplicationContext, folderUri);
			if (documentFile is null)
			{
				logger.Info("DocumentFile is null. Check if the URI is valid and permissions are granted.");
				return Task.FromResult(epubFiles);
			}
			if (documentFile is not null && documentFile.Exists() && documentFile.IsDirectory)
			{
				var listFiles = documentFile.ListFiles();
				if (listFiles is null)
				{
					logger.Info("No files found in the folder.");
					return Task.FromResult(epubFiles);
				}
				foreach (var file in listFiles)
				{
					if (file.IsFile && file.Name is not null && file.Name.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
					{
						var tempUri = file.Uri?.ToString();
						if (tempUri is null)
						{
							logger.Info("File URI is null.");
							continue;
						}
						epubFiles.Add(tempUri);
					}
				}
			}
		}
		catch (Exception ex)
		{
			logger.Info($"Error enumerating files: {ex.Message}");
		}
		return Task.FromResult(epubFiles);
	}

	public async Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath)
	{
		try
		{
			Uri? epubUri = Uri.Parse(epubFilePath);
			ArgumentNullException.ThrowIfNull(Platform.CurrentActivity?.ApplicationContext);
			var contentResolver = Platform.CurrentActivity.ApplicationContext.ContentResolver;
			ArgumentNullException.ThrowIfNull(contentResolver);

			if (epubUri is null)
			{
				logger.Info("Invalid EPUB file path.");
				return Stream.Null;
			}

			using var inputStream = contentResolver.OpenInputStream(epubUri);
			if (inputStream is not null)
			{
				var reader = new StreamReader(inputStream);
				var stream = new MemoryStream();
				await inputStream.CopyToAsync(stream);
				stream.Position = 0; // Reset position to the beginning of the stream
				return stream;
			}
		}
		catch (Exception ex)
		{
			logger.Info($"Error performing operation on {epubFilePath}: {ex.Message}");
		}
		return Stream.Null;
	}

	public async Task<string> PickFolder()
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
			if (resultCode == Result.Ok && data != null && data.Data != null)
			{
				Uri treeUri = data.Data;
				// Persist the URI access permissions
				ArgumentNullException.ThrowIfNull(Platform.CurrentActivity?.ApplicationContext);
				var contentResolver = Platform.CurrentActivity.ApplicationContext.ContentResolver;
				ArgumentNullException.ThrowIfNull(contentResolver);
				contentResolver.TakePersistableUriPermission(treeUri, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
				var treeTempUri = treeUri.ToString();
				if (string.IsNullOrEmpty(treeTempUri))
				{
					logger.Info("Picked folder URI is empty.");
					folderPickedTcs?.SetResult("null");
					return;
				}
				folderPickedTcs?.SetResult(treeTempUri);
			}
			else
			{
				folderPickedTcs?.SetResult("null");
				logger.Info("Folder picking was cancelled or failed.");
			}
		}
	}
}

