using EpubReader.ViewModels;

namespace EpubReader.Views;

public partial class LibraryPage : ContentPage
{
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