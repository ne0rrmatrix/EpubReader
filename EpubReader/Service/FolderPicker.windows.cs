using EpubReader.Interfaces;
using Windows.Storage;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;
using WindowsFolderPicker = Windows.Storage.Pickers.FolderPicker;

namespace EpubReader.Service;
public partial class FolderPicker : IFolderPicker
{
	StorageFolder? pickedFolder;
	static Window window => App.Current?.Windows[0] ?? throw new InvalidOperationException("Current window is null");
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FolderPicker));
	public async Task<string> PickFolderAsync()
	{
		var folderPicker = new WindowsFolderPicker();

		// Get the current window's HWND by passing in the Window object
		var hwnd = GetWindowsHandle();

		// Associate the HWND with the file picker
		WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

		var result = await folderPicker.PickSingleFolderAsync();
		pickedFolder = result;
		return result.Path;
	}

	public async Task<List<string>> EnumerateEpubFilesInFolderAsync(string? folderUri)
	{
		List<string> epubFiles = [];
		if (pickedFolder is null)
		{
			logger.Info("No folder picked.");
			return epubFiles;
		}
		if (string.IsNullOrEmpty(folderUri))
		{
			logger.Info("No folder URI provided.");
			return epubFiles;
		}
		try
		{
			var files = await pickedFolder.GetFilesAsync();
			foreach (var file in files.Where(x => x.FileType.Equals(".epub", StringComparison.OrdinalIgnoreCase)))
			{
				epubFiles.Add(file.Path);
			}
		}
		catch (Exception ex)
		{
			logger.Info($"Error enumerating files: {ex.Message}");
		}
		return epubFiles;
	}

	static IntPtr GetWindowsHandle()
	{
		if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window mauiWinUIWindow)
		{
			// On Windows, the PlatformView of a MAUI Window's handler is a Microsoft.UI.Xaml.Window
			// From this, you can get the HWND using WinRT.Interop.WindowNative.GetWindowHandle
			return WinRT.Interop.WindowNative.GetWindowHandle(mauiWinUIWindow);
		}
		return IntPtr.Zero;
	}

	public async Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath)
	{
		var file = await StorageFile.GetFileFromPathAsync(epubFilePath);
		if (file is not null)
		{
			return await file.OpenStreamForReadAsync();

		}
		return Stream.Null; // Return an empty stream if the file is not found or cannot be opened
	}
}
