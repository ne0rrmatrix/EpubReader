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

	/// <summary>
	/// Handles the back button press event, navigating to the home page instead of using default back navigation.
	/// </summary>
	/// <returns>True to indicate the back button press was handled.</returns>
	protected override bool OnBackButtonPressed()
	{
		Dispatcher.Dispatch(async () =>
		{
			try
			{
				while (Navigation.NavigationStack.Count > 1)
				{
					Navigation.RemovePage(Navigation.NavigationStack[1]);
				}
				Shell.SetNavBarIsVisible(this, true);
				Shell.SetTabBarIsVisible(this, true);
				await Dispatcher.DispatchAsync(() => Shell.Current.GoToAsync(".."));
			}
			catch (Exception ex)
			{
				Trace.TraceError($"Failed to navigate to home page: {ex.Message}");
			}
		});
		return true;
	}
}