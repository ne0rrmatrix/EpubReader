namespace EpubReader.Views;

public partial class CalibrePage : ContentPage
{
	static readonly ILogger logger = AppLogger.CreateLogger<CalibrePage>();
	CalibrePageViewModel viewModel => (CalibrePageViewModel)BindingContext;

	public CalibrePage(CalibrePageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

    async void OnSearchBarTextChanged(object? sender, TextChangedEventArgs? e)
	{
		if (e is null)
		{
			logger.Warn("TextChangedEventArgs is null, cannot process search.");
			return;
		}

		try
		{
          await viewModel.SearchBooksAsync(e.NewTextValue);
		}
       catch (OperationCanceledException)
		{
			logger.Info("Calibre search was cancelled.");
		}
	}

	async void OnFeedSelectionChanged(object? sender, EventArgs e)
	{
		if (sender is not Picker { SelectedItem: OpdsFeedSelection selection })
		{
			return;
		}

		try
		{
			await viewModel.SelectFeedAsync(selection);
		}
		catch (OperationCanceledException)
		{
			logger.Info("Calibre feed selection was cancelled.");
		}
	}
}