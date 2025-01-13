using EpubReader.Models;

namespace EpubReader.Interfaces;

public interface IDb
{
	public Task<Book> GetBook(string title, CancellationToken cancellationToken = default);
	public Task<List<Book>> GetAllBooks(CancellationToken cancellationToken = default);
	public Task<Settings> GetSettings(CancellationToken cancellationToken = default);
	public Task<FileData?> GetFileData(CancellationToken cancellationToken = default);
	public Task<List<FileData>> GetAllFileData(CancellationToken cancellationToken = default);

	public Task SaveBookData(Book book, CancellationToken cancellationToken = default);
	public Task SaveSettings(Settings settings, CancellationToken cancellationToken = default);

	public Task SaveFileData(FileData fileData, CancellationToken cancellationToken = default);
	public Task RemoveAllSettings(CancellationToken cancellationToken = default);
	public Task RemoveAllBooks(CancellationToken cancellationToken = default);
	public Task RemoveBook(Book book, CancellationToken cancellationToken = default);
	public Task RemoveFileData(FileData? fileData, CancellationToken cancellationToken = default);
}
