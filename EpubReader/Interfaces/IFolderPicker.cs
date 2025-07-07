namespace EpubReader.Interfaces;
public interface IFolderPicker
{
	Task<string> PickFolder();
	Task<List<string>> EnumerateEpubFilesInFolderAsync(string? folderUri);

	Task<Stream> PerformFileOperationOnEpubAsync(string epubFilePath);
}
