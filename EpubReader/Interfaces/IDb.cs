using EpubReader.Models;

namespace EpubReader.Interfaces;

public interface IDb
{
	Task<Book> GetBook(string title, CancellationToken cancellationToken = default);
	Task<List<Book>> GetAllBooks(CancellationToken cancellationToken = default);
	Task<Settings> GetSettings(CancellationToken cancellationToken = default);
	Task SaveBookData(Book book, CancellationToken cancellationToken = default);
	Task SaveSettings(Settings settings, CancellationToken cancellationToken = default);
	Task RemoveAllSettings(CancellationToken cancellationToken = default);
	Task RemoveAllBooks(CancellationToken cancellationToken = default);
	Task RemoveBook(Book book, CancellationToken cancellationToken = default);
}
