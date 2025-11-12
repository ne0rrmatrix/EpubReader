using EpubReader.Interfaces;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

/// <summary>
/// Represents a page in the application that displays a library of items.
/// </summary>
/// <remarks>The <see cref="LibraryPage"/> class is a part of the user interface that provides a view for
/// displaying and interacting with a collection of items managed by a <see cref="LibraryViewModel"/>. It is designed to
/// be used within a navigation context and ensures that the navigation bar is visible when the page is
/// displayed.</remarks>
public partial class LibraryPage : ContentPage
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(LibraryPage));
	readonly IDb db;
	ViewModels.LibraryViewModel viewModel => (ViewModels.LibraryViewModel)BindingContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="LibraryPage"/> class with the specified view model.
	/// </summary>
	/// <remarks>This constructor sets up the library page by initializing its components and setting the data
	/// context to the provided view model.</remarks>
	/// <param name="viewModel">The view model that provides data and commands for the library page. Cannot be <see langword="null"/>.</param>
	public LibraryPage(LibraryViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db ?? throw new ArgumentNullException(nameof(db), "Database cannot be null");
	}

	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		Shell.SetNavBarIsVisible(this, true);
		
		if (viewModel.Books is not null && viewModel.Books.Count > 0)
		{
			logger.Info("Books already loaded, skipping database fetch");
			return;
		}
		var temp = await db.GetAllBooks();
		temp.ForEach(x => x.IsInLibrary = true); // Ensure all books are marked as in library
		viewModel.Books = [.. temp];
		viewModel.AlphabeticalTitleSort();
	}
	
	/// <summary>
	/// Handles the text changed event for the search bar, updating the displayed list of books based on the search query.
	/// </summary>
	/// <remarks>If the search text is empty or consists only of whitespace, all books are displayed. Otherwise, the
	/// list is filtered to include books whose titles or authors contain the search text, ignoring case. The search
	/// results are logged for informational purposes.</remarks>
	/// <param name="sender">The source of the event, typically the search bar control.</param>
	/// <param name="e">The <see cref="TextChangedEventArgs"/> containing the event data, including the new text value.</param>
	async void OnSearchBarTextChanged(object? sender, TextChangedEventArgs? e)
	{
		if (e is null)
		{
			logger.Warn("TextChangedEventArgs is null, cannot process search.");
			return;
		}
		var books = viewModel.Books;
		var results = e.NewTextValue;
		var allBooks = await db.GetAllBooks();
		System.Diagnostics.Debug.WriteLine($"Search results: {results}");
		if (string.IsNullOrWhiteSpace(results))
		{
			books.Clear();
			if(allBooks.Count == books.Count)
			{
				logger.Info("Search text is empty, showing all books");
				return; // No need to update if already showing all books
			}
			viewModel.Books = [.. allBooks];
			logger.Info("Search text is empty, showing all books");
			return;
		}
		logger.Info($"Searching for books with title containing: {results}");
		var filteredTitles = allBooks.Where(b => b.Title.Contains(results, StringComparison.OrdinalIgnoreCase)).ToList();
		var filteredAuthors = allBooks.Where(b => b.Author.Contains(results, StringComparison.OrdinalIgnoreCase)).ToList();

		var filteredBooks = filteredTitles.Union(filteredAuthors).ToList();
		viewModel.Books = [.. filteredBooks];

	}
}