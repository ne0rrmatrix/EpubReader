using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class CalibrePage : ContentPage
{
	CalibrePageViewModel ViewModel => (CalibrePageViewModel)BindingContext;
	
	public CalibrePage(CalibrePageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
	void OnSearchBarTextChanged(object? sender, TextChangedEventArgs e)
	{
		var books = ViewModel.Books;
		var results = e.NewTextValue;
		var allBooks = ViewModel.BookList;
		ViewModel.Logger.Info($"Search results: {results}");
		if (string.IsNullOrWhiteSpace(results))
		{
			books.Clear();
			if (allBooks.Count == books.Count)
			{
				ViewModel.Logger.Info("Search text is empty, showing all books");
				return; // No need to update if already showing all books
			}
			ViewModel.Books = [.. allBooks];
			ViewModel.Logger.Info("Search text is empty, showing all books");
			return;
		}
		ViewModel.Logger.Info($"Searching for books with title containing: {results}");
		var filteredTitles = allBooks.Where(b => b.Title.Contains(results, StringComparison.OrdinalIgnoreCase)).ToList();
		var filteredAuthors = allBooks.Where(b => b.Author.Contains(results, StringComparison.OrdinalIgnoreCase)).ToList();

		var filteredBooks = filteredTitles.Union(filteredAuthors).ToList();
		ViewModel.Books = [.. filteredBooks];
	}
}