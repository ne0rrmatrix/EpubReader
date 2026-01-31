namespace EpubReader.Views;

/// <summary>
/// Code-behind for the Recent Books page.
/// </summary>
public partial class RecentBooksPage : ContentPage
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RecentBooksPage"/> class.
	/// </summary>
	public RecentBooksPage(RecentBooksViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (BindingContext is RecentBooksViewModel viewModel)
		{
			await viewModel.LoadRecentBooks();
		}
	}
}
