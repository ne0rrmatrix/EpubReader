using EpubReader.Interfaces;
using Foundation;
using UIKit;
using UniformTypeIdentifiers;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;

namespace EpubReader.Service;
public partial class FolderPicker : IFolderPicker
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FolderPicker));
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

	public Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath)
	{
		throw new NotImplementedException("PerformFileOperationOnEpubAsync is not implemented for iOS.");
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
}
