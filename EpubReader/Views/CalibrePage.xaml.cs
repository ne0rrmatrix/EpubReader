namespace EpubReader.Views;

public partial class CalibrePage : ContentPage
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(CalibrePage));
	CalibrePageViewModel viewModel => (CalibrePageViewModel)BindingContext;

	public CalibrePage(CalibrePageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
	void OnSearchBarTextChanged(object? sender, TextChangedEventArgs? e)
	{
		if (e is null)
		{
			logger.Warn("TextChangedEventArgs is null, cannot process search.");
			return;
		}
		var books = viewModel.Books.ToList();
		var results = e.NewTextValue;
		var allBooks = viewModel.BookList.ToList();
		logger.Info($"Search results: {results}");
		if (string.IsNullOrWhiteSpace(results))
		{
			books.Clear();
			if (allBooks.Count == books.Count)
			{
				logger.Info("Search text is empty, showing all books");
				return; // No need to update if already showing all books
			}
			viewModel.Books = [.. allBooks];
			viewModel.Logger.Info("Search text is empty, showing all books");
			return;
		}
		logger.Info($"Searching for books with title containing: {results}");
		var filteredTitles = allBooks.Where(b => b.Title.Contains(results, StringComparison.OrdinalIgnoreCase)).ToList();
		var filteredAuthors = allBooks.Where(b => b.Author.Contains(results, StringComparison.OrdinalIgnoreCase)).ToList();

		var filteredBooks = filteredTitles.Union(filteredAuthors).ToList();
		viewModel.Books = [.. filteredBooks];
	}
}