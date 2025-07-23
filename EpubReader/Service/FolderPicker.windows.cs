using EpubReader.Interfaces;
using Windows.Storage;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;
using WindowsFolderPicker = Windows.Storage.Pickers.FolderPicker;

namespace EpubReader.Service;

/// <summary>
/// Provides functionality to interact with the file system for selecting folders and handling EPUB files.
/// </summary>
/// <remarks>The <see cref="FolderPicker"/> class allows users to select folders through a UI dialog and perform
/// operations on EPUB files within those folders. It includes methods for picking a folder, enumerating EPUB files, and
/// opening EPUB files for reading. This class is designed to be used in applications that require folder selection and
/// file handling capabilities.</remarks>
public partial class FolderPicker : IFolderPicker
{
	StorageFolder? pickedFolder;
	static Window window => App.Current?.Windows[0] ?? throw new InvalidOperationException("Current window is null");
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FolderPicker));

	/// <summary>
	/// Prompts the user to select a folder and returns the path of the selected folder.
	/// </summary>
	/// <remarks>This method displays a folder picker dialog associated with the current window.  It allows the user
	/// to browse and select a folder, returning the path of the chosen folder.</remarks>
	/// <returns>A <see cref="string"/> representing the path of the selected folder.  Returns an empty string if no folder is
	/// selected.</returns>
	public async Task<string> PickFolderAsync()
	{
		var folderPicker = new WindowsFolderPicker();

		// Get the current window's HWND by passing in the Window object
		var hwnd = GetWindowsHandle();

		// Associate the HWND with the file picker
		WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

		var result = await folderPicker.PickSingleFolderAsync();
		pickedFolder = result;
		if(result is null)
		{
			logger.Info("No folder selected.");
			return string.Empty; // Return an empty string if no folder is selected
		}
		return result.Path;
	}

	/// <summary>
	/// Asynchronously enumerates all EPUB files in the specified folder.
	/// </summary>
	/// <remarks>This method logs informational messages if no folder is picked or if an error occurs during file
	/// enumeration.</remarks>
	/// <param name="folderUri">The URI of the folder to search for EPUB files. Can be null or empty, in which case an empty list is returned.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains a list of file paths for all EPUB files
	/// found in the folder. Returns an empty list if no EPUB files are found or if the folder URI is null or empty.</returns>
	public async Task<List<string>> EnumerateEpubFilesInFolderAsync(string? folderUri, CancellationToken cancellationToken = default)
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
				if (cancellationToken.IsCancellationRequested)
				{
					logger.Info("File enumeration cancelled by user.");
					return []; // Return what has been collected so far
				}
				epubFiles.Add(file.Path);
			}
		}
		catch (Exception ex)
		{
			logger.Info($"Error enumerating files: {ex.Message}");
		}
		return epubFiles;
	}

	/// <summary>
	/// Opens an EPUB file and returns a stream for reading its contents.
	/// </summary>
	/// <remarks>This method is asynchronous and should be awaited. It is designed to handle EPUB files
	/// specifically.</remarks>
	/// <param name="epubFilePath">The file path of the EPUB file to be opened. Must be a valid path to an existing file.</param>
	/// <returns>A <see cref="Stream"/> for reading the contents of the EPUB file.  Returns <see cref="Stream.Null"/> if the file is
	/// not found or cannot be opened.</returns>
	public async Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath, CancellationToken cancellationToken = default)
	{
		var file = await StorageFile.GetFileFromPathAsync(epubFilePath);
		if (file is not null)
		{
			return await file.OpenStreamForReadAsync();

		}
		return Stream.Null; // Return an empty stream if the file is not found or cannot be opened
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
}
