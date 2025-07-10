using EpubReader.ViewModels;

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
	/// <summary>
	/// Initializes a new instance of the <see cref="LibraryPage"/> class with the specified view model.
	/// </summary>
	/// <remarks>This constructor sets up the library page by initializing its components and setting the data
	/// context to the provided view model.</remarks>
	/// <param name="viewModel">The view model that provides data and commands for the library page. Cannot be <see langword="null"/>.</param>
	public LibraryPage(LibraryViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
	}

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        Shell.SetNavBarIsVisible(this, true);
    }
}