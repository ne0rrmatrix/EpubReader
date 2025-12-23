namespace EpubReader.Interfaces;

/// <summary>
/// Provides methods for selecting folders and performing operations on EPUB files within those folders.
/// </summary>
/// <remarks>This interface defines asynchronous methods for folder selection and EPUB file operations. It is
/// designed to be implemented by classes that interact with file systems or storage services to facilitate folder
/// picking and file manipulation tasks.</remarks>
public interface IFolderPicker
{
	/// <summary>
	/// Displays a folder picker dialog that allows the user to select a folder.
	/// </summary>
	/// <returns>A task that represents the asynchronous operation. The task result contains the path of the selected folder as a
	/// string. Returns <see langword="null"/> if the user cancels the operation or no folder is selected.</returns>
	Task<string> PickFolderAsync();

	/// <summary>
	/// Asynchronously enumerates all EPUB files in the specified folder.
	/// </summary>
	/// <param name="folderUri">The URI of the folder to search for EPUB files. Can be null, in which case an empty list is returned.</param>
	/// <returns>A task representing the asynchronous operation. The task result contains a list of file paths for all EPUB files
	/// found in the specified folder. The list will be empty if no EPUB files are found or if <paramref name="folderUri"/>
	/// is null.</returns>
	Task<List<string>> EnumerateEpubFilesInFolderAsync(string? folderUri, CancellationToken cancellationToken = default);

	/// <summary>
	/// Asynchronously performs a file operation on the specified EPUB file and returns the resulting stream.
	/// </summary>
	/// <param name="epubFilePath">The path to the EPUB file on which the operation will be performed. Cannot be null or empty.</param>
	/// <returns>A task representing the asynchronous operation. The task result contains a <see cref="Stream"/> of the processed
	/// EPUB file.</returns>

	Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath, CancellationToken cancellationToken = default);
}