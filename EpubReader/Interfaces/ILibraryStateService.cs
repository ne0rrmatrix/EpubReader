using System.Collections.ObjectModel;

namespace EpubReader.Interfaces;

public interface ILibraryStateService
{
	ObservableCollection<Book> Books { get; }

	Task InitializeAsync(CancellationToken token = default);
	Task RefreshAsync(CancellationToken token = default);
	Task<bool> ContainsAsync(Book book, CancellationToken token = default);
	Task AddBookAsync(Book book, CancellationToken token = default);
	Task RemoveBookAsync(Book book, CancellationToken token = default);
}
