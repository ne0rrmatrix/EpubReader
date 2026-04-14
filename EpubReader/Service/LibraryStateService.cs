using System.Collections.ObjectModel;

namespace EpubReader.Service;

public sealed class LibraryStateService(IDb db) : ILibraryStateService
{
	readonly IDb db = db;
	readonly IDispatcher dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException();
	bool isInitialized;

	public ObservableCollection<Book> Books { get; } = [];

	public async Task InitializeAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		if (isInitialized)
		{
			return;
		}

		await RefreshAsync(token);
	}

	public async Task RefreshAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		var books = await db.GetAllBooks(token);
		books.ForEach(book => book.IsInLibrary = true);
		if (!dispatcher.IsDispatchRequired)
		{
			ReplaceBooks(books);
		}
		else
		{
			await dispatcher.DispatchAsync(() => ReplaceBooks(books));
		}
		isInitialized = true;
	}

	public async Task<bool> ContainsAsync(Book book, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(book);
		await InitializeAsync(token);
		if (!dispatcher.IsDispatchRequired)
		{
			return Books.Any(existing => string.Equals(existing.Title, book.Title, StringComparison.OrdinalIgnoreCase));
		}

		var containsCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (!dispatcher.Dispatch(() =>
		{
			try
			{
				containsCompletionSource.SetResult(Books.Any(existing => string.Equals(existing.Title, book.Title, StringComparison.OrdinalIgnoreCase)));
			}
			catch (Exception ex)
			{
				containsCompletionSource.SetException(ex);
			}
		}))
		{
			throw new InvalidOperationException("Failed to dispatch work to the UI thread.");
		}

		return await containsCompletionSource.Task;
	}

	public async Task AddBookAsync(Book book, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(book);
		await InitializeAsync(token);
		if (await ContainsAsync(book, token))
		{
			return;
		}

		book.IsInLibrary = true;
		await db.SaveBookData(book, token);
		if (!dispatcher.IsDispatchRequired)
		{
			Books.Add(book);
			return;
		}

		await dispatcher.DispatchAsync(() => Books.Add(book));
	}

	public async Task RemoveBookAsync(Book book, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(book);
		await db.RemoveBook(book, token);
		Book? existing;
		if (!dispatcher.IsDispatchRequired)
		{
			existing = Books.FirstOrDefault(candidate => candidate.Id == book.Id || string.Equals(candidate.FilePath, book.FilePath, StringComparison.OrdinalIgnoreCase));
		}
		else
		{
			var existingCompletionSource = new TaskCompletionSource<Book?>(TaskCreationOptions.RunContinuationsAsynchronously);
			if (!dispatcher.Dispatch(() =>
			{
				try
				{
					existingCompletionSource.SetResult(Books.FirstOrDefault(candidate => candidate.Id == book.Id || string.Equals(candidate.FilePath, book.FilePath, StringComparison.OrdinalIgnoreCase)));
				}
				catch (Exception ex)
				{
					existingCompletionSource.SetException(ex);
				}
			}))
			{
				throw new InvalidOperationException("Failed to dispatch work to the UI thread.");
			}

			existing = await existingCompletionSource.Task;
		}

		if (existing is not null)
		{
			if (!dispatcher.IsDispatchRequired)
			{
				Books.Remove(existing);
				return;
			}

			await dispatcher.DispatchAsync(() => Books.Remove(existing));
		}
	}

	void ReplaceBooks(IEnumerable<Book> books)
	{
		Books.Clear();
		foreach (var book in books)
		{
			Books.Add(book);
		}
	}
}