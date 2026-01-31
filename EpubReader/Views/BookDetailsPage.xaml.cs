using System.Diagnostics;

namespace EpubReader.Views;

public partial class BookDetailsPage : ContentPage
{
	public BookDetailsPage(BookDetailsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
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