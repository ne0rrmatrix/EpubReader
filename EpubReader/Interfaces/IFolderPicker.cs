namespace EpubReader.Interfaces;
public interface IFolderPicker
{
	Task<string> PickFolderAsync();
	Task<List<string>> EnumerateEpubFilesInFolderAsync(string? folderUri);

	Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath);
}
