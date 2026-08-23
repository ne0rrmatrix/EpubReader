using System.Diagnostics;

namespace EpubReader.Views;

public partial class BookDetailsPage : ContentPage
{
	public BookDetailsPage(BookDetailsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		Shell.SetNavBarIsVisible(this, true);
		Shell.SetTabBarIsVisible(this, true);
	}
}