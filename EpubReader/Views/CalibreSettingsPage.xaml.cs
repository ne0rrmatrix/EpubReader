namespace EpubReader.Views;

public partial class CalibreSettingsPage : Popup<bool>
{
	readonly CalibreSettingsPageViewModel viewModel;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(CalibreSettingsPage));

	public CalibreSettingsPage(CalibreSettingsPageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = this.viewModel = viewModel;
		Loaded += CurrentPage_Loaded;
		Unloaded += CurrentPage_Unloaded;
		this.viewModel.CloseRequested += ViewModel_CloseRequested;
	}

	async void CurrentPage_Loaded(object? sender, EventArgs? e)
	{
		try
		{
			await viewModel.InitializeAsync();
		}
		catch (Exception ex)
		{
			logger.Error($"Failed to initialize Calibre settings popup: {ex.Message}");
		}
	}

	void CurrentPage_Unloaded(object? sender, EventArgs? e)
	{
		Loaded -= CurrentPage_Loaded;
		Unloaded -= CurrentPage_Unloaded;
		viewModel.CloseRequested -= ViewModel_CloseRequested;
	}

	async void ViewModel_CloseRequested(object? sender, bool result)
	{
		try
		{
			await CloseAsync(result);
		}
		catch (Exception ex)
		{
			logger.Error($"Failed to close Calibre settings popup: {ex.Message}");
		}
	}
}