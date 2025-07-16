using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Models;
using EpubReader.Util;
using EpubReader.Views;

namespace EpubReader.ViewModels;

/// <summary>
/// Represents a view model for a book, providing properties and methods to manage book-related data and interactions.
/// </summary>
/// <remarks>The <see cref="BookViewModel"/> class is responsible for handling the presentation logic related to a
/// book, including managing the web view content source, cover image, and UI state such as navigation menu visibility.
/// It also supports applying query attributes to update its state based on external input.</remarks>
public partial class BookViewModel : BaseViewModel, IQueryAttributable
{
#pragma warning disable S1075 // URIs should not be hardcoded
#if ANDROID || WINDOWS
	const string url = "https://demo/index.html";
#elif IOS || MACCATALYST
	const string url = "app://demo/index.html";
#endif
#pragma warning restore S1075 // URIs should not be hardcoded

	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();

	/// <summary>
	/// Gets or sets the source of the web view content.
	/// </summary>
	[ObservableProperty]
	public partial WebViewSource Source { get; set; } = new UrlWebViewSource
	{
		Url = url,
	};

	/// <summary>
	/// Gets or sets the cover image for the item.
	/// </summary>
	[ObservableProperty]
	public partial ImageSource CoverImage { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the view model is currently active.
	/// </summary>
	[ObservableProperty]
	public partial bool IsActive { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the popup is currently active.
	/// </summary>
	[ObservableProperty]
	public partial bool isPopupActive { get; set; } = false;

	/// <summary>
	/// Gets or sets a value indicating whether the navigation menu is visible.
	/// </summary>
	[ObservableProperty]
	public partial bool IsNavMenuVisible { get; set; } = true;

	/// <summary>
	/// Initializes a new instance of the <see cref="BookViewModel"/> class.
	/// </summary>
	/// <remarks>This constructor initializes the <see cref="BookViewModel"/> with default values.</remarks>
	public BookViewModel()
	{
		Press();
	}

	/// <summary>
	/// Applies query attributes to the current instance, extracting and setting the book details.
	/// </summary>
	/// <remarks>This method updates the current instance with the book details provided in the query. It sets the
	/// <see cref="Book"/> property and initializes the <see cref="CoverImage"/> from the book's cover image
	/// data.</remarks>
	/// <param name="query">A dictionary containing query parameters, where the key "Book" should map to a <see cref="Book"/> object.</param>
	/// <exception cref="InvalidOperationException">Thrown if the "Book" entry in the query does not contain a valid <see cref="Book"/> object or if the book's cover
	/// image is null.</exception>
	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("Book", out var bookObj) && bookObj is Book book)
		{
			Book = book;
			streamExtensions.SetBook(Book);
			var bytes = book.CoverImage ?? throw new InvalidOperationException("CoverImage is null");
			CoverImage = ImageSource.FromStream(() => new MemoryStream(bytes));
		}
	}

	/// <summary>
	/// Displays a popup using the specified view model.
	/// </summary>
	/// <remarks>This method activates a popup and awaits its completion. If the popup is dismissed by tapping
	/// outside of it, the popup is deactivated.</remarks>
	/// <returns></returns>
	[RelayCommand]
	async Task ShowPopup(CancellationToken cancellation = default)
	{
		isPopupActive = true;
		var popup = new SettingsPage(new SettingsPageViewModel());
		PopupOptions options = new PopupOptions
		{
			CanBeDismissedByTappingOutsideOfPopup = true,
		};
		
		IPopupResult<bool> result = await Shell.Current.ShowPopupAsync<bool>(popup, options, cancellation);
		if (result.WasDismissedByTappingOutsideOfPopup)
		{
			System.Diagnostics.Debug.WriteLine("Popup was dismissed by tapping outside of it.");
			isPopupActive = false;
		}
		else
		{
			System.Diagnostics.Debug.WriteLine("Popup was closed by other means.");
			isPopupActive = false;
		}
	}

	/// <summary>
	/// Toggles the visibility of the navigation menu and updates the status bar visibility accordingly.
	/// </summary>
	/// <remarks>On Android, this method also adjusts the status bar visibility to match the navigation menu's
	/// visibility.</remarks>
	[RelayCommand]
	public void Press()
	{
#if ANDROID
		Service.StatusBarExtensions.SetStatusBarsHidden(IsNavMenuVisible);
#endif
		IsNavMenuVisible = !IsNavMenuVisible;
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, IsNavMenuVisible);
	}
}
