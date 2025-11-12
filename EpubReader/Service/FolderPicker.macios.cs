using Foundation;
using UIKit;
using UniformTypeIdentifiers;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;

namespace EpubReader.Service;

/// <summary>
/// Provides functionality to select a folder and perform operations on EPUB files within the selected folder.
/// </summary>
/// <remarks>The <see cref="FolderPicker"/> class allows users to pick a folder using a document picker interface
/// and perform operations such as enumerating EPUB files within the selected folder. It is designed to work with
/// iOS-specific APIs and requires a valid UIViewController to present the picker.</remarks>
public partial class FolderPicker : IFolderPicker
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FolderPicker));
	
	/// <summary>
	/// Asynchronously presents a folder picker dialog to the user and returns the path of the selected folder.
	/// </summary>
	/// <remarks>This method displays a folder picker using a <see cref="UIDocumentPickerViewController"/>
	/// configured to allow selection of a single folder. The method completes when the user selects a folder or dismisses
	/// the picker.</remarks>
	/// <returns>A task that represents the asynchronous operation. The task result contains the path of the selected folder as a
	/// string. If the picker is dismissed without selecting a folder, the result is <see langword="null"/>.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the picker's PresentationController is <see langword="null"/>. Ensure the picker is presented from a
	/// valid UIViewController.</exception>
	public async Task<string> PickFolderAsync()
	{
		var allowedTypes = new UTType[]
		{
		UTTypes.Folder
		};

		var picker = new UIDocumentPickerViewController(allowedTypes, false)
		{
			AllowsMultipleSelection = false
		};

		var tcs = new TaskCompletionSource<string>();
		var pickerDelegate = new PickerDelegate
		{
			PickHandler = urls => GetFileResults(urls, tcs)
		};
		picker.Delegate = pickerDelegate;

		var dismissHandler = new Action(() => GetFileResults(null!, tcs));
		var delegateController = new UIPresentationControllerDelegate(dismissHandler);
		if(picker.PresentationController is null)
		{
			throw new InvalidOperationException("Picker's PresentationController is null. Ensure the picker is presented from a valid UIViewController.");
		}
		picker.PresentationController.Delegate = delegateController;

		var parentController = Platform.GetCurrentUIViewController();
		parentController?.PresentViewController(picker, true, null);

		return await tcs.Task;
	}

	/// <summary>
	/// Asynchronously enumerates all EPUB files in the specified folder.
	/// </summary>
	/// <remarks>This method logs information about any errors encountered during the enumeration process, such as
	/// invalid folder URIs or inaccessible folder contents.</remarks>
	/// <param name="folderUri">The URI of the folder to search for EPUB files. This must be a valid file URI. If null or empty, an empty list is
	/// returned.</param>
	/// <returns>A task representing the asynchronous operation. The task result contains a list of file paths to EPUB files found
	/// in the specified folder. The list will be empty if no EPUB files are found or if the folder cannot be accessed.</returns>
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
            // Convert the string URL to NSUrl
            NSUrl folderUrl = new(folderUri);
            if (!folderUrl.IsFileUrl)
            {
                return Task.FromResult(epubFiles);
            }

            // Get access to file system
            NSFileManager fileManager = NSFileManager.DefaultManager;

			// Check if folder exists and is accessible
			string folderPath = folderUrl.Path ?? string.Empty; // Convert NSUrl to string path
			string[]? contents = fileManager.GetDirectoryContent(folderPath, out NSError? error);

            if (error is not null || contents is null)
            {
				logger.Info($"Error accessing folder contents: {error?.LocalizedDescription}");
				return Task.FromResult(epubFiles);
            }
			
            // Enumerate through directory contents
            foreach (var filePath in contents)
            {
                NSUrl fileUrl = NSUrl.FromFilename(filePath);

                // Check if it's a file
                bool isDirectory = true;
                if (fileUrl.Path is null)
                {
                    logger.Info("File URL is null.");
                    continue;
                }
                if (fileManager.FileExists(fileUrl.Path, ref isDirectory) && 
					!isDirectory && 
					fileUrl.LastPathComponent?.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) == true)
					{
						epubFiles.Add(fileUrl.Path);
					}
            }
        }
        catch (Exception ex)
        {
            logger.Info($"Error enumerating EPUB files: {ex.Message}");
        }

        return Task.FromResult(epubFiles);
    }

	/// <summary>
	/// Asynchronously performs a file operation on the specified EPUB file.
	/// </summary>
	/// <param name="epubFilePath">The path to the EPUB file on which the operation will be performed. Cannot be null or empty.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Stream"/> of the EPUB
	/// file.</returns>
	/// <exception cref="NotImplementedException">Thrown if the method is not implemented for the current platform.</exception>
	public Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath, CancellationToken cancellationToken = default)
	{
		throw new NotImplementedException("PerformFileOperationOnEpubAsync is not implemented for iOS.");
	}

	static void GetFileResults(NSUrl[] urls, TaskCompletionSource<string> tcs)
	{
		try
		{
			tcs.TrySetResult(urls?[0]?.ToString() ?? "");
		}
		catch (Exception ex)
		{
			tcs.TrySetException(ex);
		}
	}

	internal class UIPresentationControllerDelegate : UIAdaptivePresentationControllerDelegate
	{
		Action? dismissHandler;

		internal UIPresentationControllerDelegate(Action dismissHandler)
			=> this.dismissHandler = dismissHandler;

		public override void DidDismiss(UIPresentationController presentationController)
		{
			dismissHandler?.Invoke();
			dismissHandler = null;
		}

		protected override void Dispose(bool disposing)
		{
			dismissHandler?.Invoke();
			base.Dispose(disposing);
		}
	}

	class PickerDelegate : UIDocumentPickerDelegate
	{

		public Action<NSUrl[]>? PickHandler { get; set; }

		public override void WasCancelled(UIDocumentPickerViewController controller)
			=> PickHandler?.Invoke(null!);

		public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
			=> PickHandler?.Invoke(urls);

		public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
			=> PickHandler?.Invoke([url]);
	}
}
