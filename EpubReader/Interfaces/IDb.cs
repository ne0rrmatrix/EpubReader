using EpubReader.Models;

namespace EpubReader.Interfaces;

public interface IDb
{
	public Task<Settings> GetSettings(CancellationToken cancellationToken = default);

	public Task<List<FileData>> GetFileData(CancellationToken cancellationToken = default);

	public Task<FileData?> GetFileData(int id, CancellationToken cancellationToken = default);

	public Task SaveSettings(Settings settings, CancellationToken cancellationToken = default);

	public Task SaveFileData(FileData fileData, CancellationToken cancellationToken = default);
	public Task RemoveSettingsData(int id, CancellationToken cancellationToken = default);
	public Task RemoveFileData(Book book, CancellationToken cancellationToken = default);

	public Task UpdateBook(FileData fileData, CancellationToken cancellationToken = default);
}
