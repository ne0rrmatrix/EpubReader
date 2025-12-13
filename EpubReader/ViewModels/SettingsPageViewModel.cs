using System.Diagnostics;
using AsyncAwaitBestPractices;

namespace EpubReader.ViewModels;

/// <summary>
/// Represents the view model for the settings page, providing access to available fonts and color schemes.
/// </summary>
/// <remarks>This class is responsible for managing the collections of fonts and color schemes that can be used in
/// an EPUB document. It provides properties to access these collections, which are initialized with a predefined set of
/// options.</remarks>
public partial class SettingsPageViewModel : BaseViewModel
{
	readonly AuthenticationService authenticationService;
	readonly ISyncService syncService;
	readonly List<EpubFonts> fonts = [
		new EpubFonts { FontFamily = "Arial" },
		new EpubFonts { FontFamily = "Times New Roman" },
		new EpubFonts { FontFamily = "Verdana" },
		new EpubFonts { FontFamily = "Courier New" },
		new EpubFonts { FontFamily = "Georgia" },
		new EpubFonts { FontFamily = "Tahoma" },
		new EpubFonts { FontFamily = "Trebuchet MS" },
		new EpubFonts { FontFamily = "Comic Sans MS" },
		new EpubFonts { FontFamily = "Helvetica" },
		// Dyslexic-friendly fonts (users must have these installed or bundled)
		new EpubFonts { FontFamily = "OpenDyslexic3Regular" },
];
	readonly List<ColorScheme> colorSchemes =
		[
			new ColorScheme() { Name = "Material Light", BackgroundColor = "#FAFAFA", TextColor = "#111827" },
			new ColorScheme() { Name = "Sepia", BackgroundColor = "#F4E7D3", TextColor = "#5B4636" },
			new ColorScheme() { Name = "Warm Paper", BackgroundColor = "#FFF8E1", TextColor = "#3E2723" },
			new ColorScheme() { Name = "Dim", BackgroundColor = "#1E1E1E", TextColor = "#E6E1DA" },
			new ColorScheme() { Name = "AMOLED", BackgroundColor = "#000000", TextColor = "#FFFFFF" },
			new ColorScheme() { Name = "Solarized Light", BackgroundColor = "#FDF6E3", TextColor = "#586E75" },
			// Accessibility-focused themes
			new ColorScheme() { Name = "High Contrast", BackgroundColor = "#000000", TextColor = "#FFFF00" },
			new ColorScheme() { Name = "Colorblind Friendly", BackgroundColor = "#F5F7FA", TextColor = "#0B3D91" },
			new ColorScheme() { Name = "Dyslexic Friendly", BackgroundColor = "#F7F7F7", TextColor = "#0B0B0B" }
		];

	[ObservableProperty]
	public partial bool IsAuthenticated { get; set; }

	[ObservableProperty]
	public partial bool IsNotAuthenticated { get; set; } = true;

	[ObservableProperty]
	public partial bool IsLocalOnly { get; set; }

	[ObservableProperty]
	public partial string AuthStatusText { get; set; } = string.Empty;

	/// <summary>
	/// Gets the collection of available color schemes.
	/// </summary>
	public List<ColorScheme> ColorSchemes => colorSchemes;

	/// <summary>
	/// Gets the collection of fonts used in the EPUB document.
	/// </summary>
	public List<EpubFonts> Fonts => fonts;

	/// <summary>
	/// Initializes a new instance of the <see cref="SettingsPageViewModel"/> class.
	/// Note: long-running async work is NOT started here. Call <see cref="InitializeAsync"/> from the view (OnAppearing/Loaded).
	/// </summary>
	public SettingsPageViewModel(AuthenticationService authenticationService, ISyncService syncService)
	{
		this.authenticationService = authenticationService;
		this.syncService = syncService;
		this.authenticationService.AuthStateChanged += OnAuthStateChanged;
		InitializeAsync().SafeFireAndForget(onException: ex =>
		{
			Debug.WriteLine($"Error during SettingsPageViewModel initialization: {ex}");
		});
	}
	protected override void Dispose(bool disposing)
	{
		authenticationService.AuthStateChanged -= OnAuthStateChanged;
		base.Dispose(disposing);
	}

	/// <summary>
	/// Performs async initialization for the view model. Call this from a UI lifecycle event (Page.OnAppearing / Loaded).
	/// Returns the underlying task so callers can await if desired.
	/// </summary>
	public Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		// Forward to the existing method which contains the actual async logic.
		return LoadAuthStatusAsync(cancellationToken);
	}

	async void OnAuthStateChanged(object? sender, bool isAuthenticated)
	{
		await LoadAuthStatusAsync();
	}

	public async Task LoadAuthStatusAsync(CancellationToken cancellationToken = default)
	{
		var isAuth = await authenticationService.IsAuthenticatedAsync(cancellationToken);
		var isLocal = await AuthenticationService.IsLocalOnlyModeAsync(cancellationToken);

		IsAuthenticated = isAuth;
		IsNotAuthenticated = !isAuth;
		IsLocalOnly = isLocal;

		if (isAuth)
		{
			var userEmail = await authenticationService.GetCurrentUserEmailAsync(cancellationToken);
			AuthStatusText = string.IsNullOrWhiteSpace(userEmail)
				? "Signed in - Cloud sync enabled"
				: $"Signed in as:\n{userEmail}";
		}
	}

	[RelayCommand]
	async Task SignInAsync(CancellationToken cancellationToken = default)
	{
		await LoadAuthStatusAsync(cancellationToken);
		var navigationPage = new LoginPage(new LoginPageViewModel(authenticationService));
		await Shell.Current.Navigation.PushModalAsync(navigationPage, true);
	}

	[RelayCommand]
	async Task SignOutAsync(CancellationToken cancellationToken = default)
	{
		var page = Application.Current?.Windows[0]?.Page;
		if (page is null)
		{
			return;
		}

		var result = await page.DisplayAlertAsync(
			"Sign Out",
			"Are you sure you want to sign out? This will disable cloud sync and switch to local-only mode.",
			"Sign Out",
			"Cancel");

		if (result)
		{
			await authenticationService.SignOutAsync(cancellationToken);
			await LoadAuthStatusAsync(cancellationToken);
		}
	}

	[RelayCommand]
	async Task DeleteCloudDataAsync(CancellationToken cancellationToken = default)
	{
		var page = Application.Current?.Windows[0]?.Page;
		if (page is null)
		{
			return;
		}

		// Only allow when authenticated and not in local-only mode
		await LoadAuthStatusAsync(cancellationToken);
		if (!IsAuthenticated || IsLocalOnly)
		{
			await ShowInfoToastAsync("Cloud delete unavailable in local-only mode");
			return;
		}

		var confirm = await page.DisplayAlertAsync(
			"Delete Cloud Data",
			"This will permanently delete your cloud-synced reading progress across all books for your account. This action cannot be undone.\n\nDo you want to continue?",
			"Delete",
			"Cancel");

		if (!confirm)
		{
			return;
		}

		try
		{
			await syncService.DeleteAllCloudDataAsync(cancellationToken);
			await ShowInfoToastAsync("Cloud data deleted");
		}
		catch (Exception ex)
		{
			await ShowErrorToastAsync($"Failed deleting cloud data: {ex.Message}");
		}
	}
}