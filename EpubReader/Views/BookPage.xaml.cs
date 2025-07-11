using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Util;
using EpubReader.ViewModels;

namespace EpubReader.Views;

/// <summary>
/// Represents a page in a book application, providing functionality for displaying and interacting with book content.
/// </summary>
/// <remarks>The <see cref="BookPage"/> class is responsible for managing the user interface and interactions for
/// a single page of a book. It handles navigation, animations, and JavaScript interactions within a web view. The page
/// is initialized with a view model and a database interface, which are used for data binding and data operations,
/// respectively.</remarks>
public partial class BookPage : ContentPage
{
	const string externalLinkPrefix = "https://runcsharp.jump/?";
	const uint animationDuration = 200u;
	BookViewModel ViewModel => (BookViewModel)BindingContext;
	Book book => ViewModel.Book;
	readonly IDb db;
	readonly WebViewHelper webViewHelper;

	/// <summary>
	/// Initializes a new instance of the <see cref="BookPage"/> class with the specified view model and database.
	/// </summary>
	/// <remarks>This constructor sets up the page's data binding context and initializes necessary components. It
	/// also registers message handlers for JavaScript and settings messages using a weak reference messenger.</remarks>
	/// <param name="viewModel">The view model that provides data binding for the page.</param>
	/// <param name="db">The database interface used for data operations within the page.</param>
	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		webViewHelper = new(webView);

		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, async (r, m) => await HandleJavascriptAsync(m.Value));
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => { await webViewHelper.OnSettingsClickedAsync(); UpdateUiAppearance(); });
	}

	/// <summary>
	/// Handles the actions to be performed when the page is disappearing.
	/// </summary>
	/// <remarks>This method unregisters all messages for the current instance and clears the toolbar items if the
	/// popup is not active. It also ensures the navigation bar is visible. Overrides the base <see cref="OnDisappearing"/>
	/// method.</remarks>
	protected override void OnDisappearing()
	{
		if (!ViewModel.isPopupActive)
		{
			WeakReferenceMessenger.Default.UnregisterAll(this);
			Shell.Current.ToolbarItems.Clear();
			Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
		}
		base.OnDisappearing();
	}

	/// <summary>
	/// Handles the tap event on the grid area, triggering animations for translation, scaling, and fading.
	/// </summary>
	/// <remarks>This method initiates a sequence of animations on the grid area when it is tapped. The animations
	/// include translating the grid, scaling it down, and fading it. The translation distance varies depending on the
	/// operating system, with a larger translation on iOS and Android platforms.</remarks>
	/// <param name="sender">The source of the event, typically the grid area that was tapped.</param>
	/// <param name="e">The event data associated with the tap event.</param>
	async void GridArea_Tapped(object sender, EventArgs e)
	{
		ViewModel.Press();
		var width = this.Width * 0.4;
		if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
		{
			width = this.Width * 0.8;
		}
		await grid.TranslateTo(-width, this.Height * 0.1, animationDuration, Easing.CubicIn).ConfigureAwait(false);
		await grid.ScaleTo(0.8, animationDuration).ConfigureAwait(false);
		await grid.FadeTo(0.8, animationDuration).ConfigureAwait(false);
	}

	/// <summary>
	/// Handles the navigation event for the web view.
	/// </summary>
	/// <remarks>This method is triggered after the web view has completed navigation to a new page. It updates the
	/// UI to reflect the navigation state.</remarks>
	/// <param name="sender">The source of the event, typically the web view control.</param>
	/// <param name="e">The event data containing information about the navigation event.</param>
	async void webView_Navigated(object? sender, WebNavigatedEventArgs e)
	{
		await webViewHelper.LoadPageAsync(pageLabel, book);
		Shimmer.IsActive = false;
	}

	/// <summary>
	/// Handles the Loaded event for the current page, initializing toolbar items for each chapter in the book.
	/// </summary>
	/// <param name="sender">The source of the event.</param>
	/// <param name="e">The event data.</param>
	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
	}

	/// <summary>
	/// Asynchronously handles a JavaScript action based on the provided URL.
	/// </summary>
	/// <remarks>This method processes the URL to determine the appropriate JavaScript action and updates the UI
	/// accordingly. It attempts to handle both internal and external links before executing a specific web view
	/// action.</remarks>
	/// <param name="url">The URL that determines the JavaScript action to be handled. Cannot be null or empty.</param>
	/// <returns></returns>
	async Task HandleJavascriptAsync(string url)
	{
		await TryHandleInternalLinkAsync(url);
		await BookPage.TryHandleExternalLinkAsync(url);
		var methodName = GetMethodNameFromUrl(url);
		await HandleWebViewActionAsync(methodName);
		UpdateUiAppearance();
	}

	/// <summary>
	/// Handles the navigation event for the web view, intercepting specific URLs for custom processing.
	/// </summary>
	/// <remarks>This method cancels navigation if the URL contains the substring "runcsharp", ignoring case, and
	/// processes the URL asynchronously.</remarks>
	/// <param name="sender">The source of the event, typically the web view control.</param>
	/// <param name="e">The <see cref="WebNavigatingEventArgs"/> containing event data, including the URL being navigated to.</param>
	async void webView_Navigating(object? sender, WebNavigatingEventArgs e)
	{
		var url = e.Url;
		
		if (!url.Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			return;
		}
		
		e.Cancel = true;
		await HandleJavascriptAsync(e.Url);
	}
	async Task TryHandleInternalLinkAsync(string url)
	{	
		if (!url.Contains("https://runcsharp.jump/?https://demo/", StringComparison.InvariantCultureIgnoreCase))
		{
			return;
		}
		
		var urlParts = url.Split('?')[1].Split('#')[0];
		if(!string.IsNullOrEmpty(urlParts))
		{
			var chapter = book.Chapters.Find(chapter => chapter.FileName.Contains(Path.GetFileName(urlParts), StringComparison.OrdinalIgnoreCase));
			if (chapter is null)
			{
				return;
			}
			book.CurrentChapter = book.Chapters.IndexOf(chapter);
			await webView.EvaluateJavaScriptAsync($"loadPage(\"{chapter.FileName}\")");
			db.UpdateBookMark(book);
		}
	}
	static async Task TryHandleExternalLinkAsync(string url)
	{
		if (!url.Contains(externalLinkPrefix, StringComparison.OrdinalIgnoreCase) || url.Contains("https://demo") || url.Contains("app://demo"))
		{
			return;
		}

		var urlParts = url.Split('?');
		if (urlParts.Length <= 1)
		{
			return;
		}

		var queryString = urlParts[1].Replace("http://", "https://");
		if (!string.IsNullOrEmpty(queryString) && queryString.Contains("https://"))
		{
			await Launcher.OpenAsync(queryString);
		}
	}

	static string GetMethodNameFromUrl(string url)
	{
		var urlParts = url.Split('.');
		if (urlParts.Length < 2)
		{
			return string.Empty;
		}

		var funcToCall = urlParts[1].Split('?');
		if (string.IsNullOrEmpty(funcToCall[0]) || funcToCall[0].Length <= 1)
		{
			return string.Empty;
		}

		return funcToCall[0][..^1]; // Assumes format like "method()"
	}

	async Task HandleWebViewActionAsync(string methodName)
	{
		switch (methodName.ToLowerInvariant())
		{
			case "next":
				await webViewHelper.Next(pageLabel, book);
				break;
			case "prev":
				await webViewHelper.Prev(pageLabel, book);
				break;
			case "menu":
				GridArea_Tapped(this, EventArgs.Empty);
				break;
			case "pageload":
				await webViewHelper.OnSettingsClickedAsync();
				UpdateUiAppearance();
				pageLabel.Text = await GetCurrentPageInfoAsync();
				break;
			case "updatepageinfo":
				pageLabel.Text = await GetCurrentPageInfoAsync();
				break;
		}
	}

	async Task<string> GetCurrentPageInfoAsync()
	{
		var tempPageResult = await webView.EvaluateJavaScriptAsync("getCurrentPage()");
		var tempPageCount = await webView.EvaluateJavaScriptAsync("getPageCount()");
		var pageCount = tempPageCount?.ToString();
		var result = Int32.TryParse(pageCount, out var pageCountValue) ? pageCountValue : 0;
		pageCount = pageCountValue > 0 ? result.ToString() : string.Empty;
		var currentPage = tempPageResult?.ToString();
		if (!string.IsNullOrEmpty(pageCount) || !string.IsNullOrEmpty(currentPage))
		{
			return $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty} (Page {currentPage} of {pageCount})";		
		}
		return $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
	}

	void UpdateUiAppearance()
	{
		pageLabel.IsVisible = !string.IsNullOrEmpty(pageLabel.Text);
		var settings = db.GetSettings() ?? new();
		if(string.IsNullOrEmpty(settings.BackgroundColor))
		{
			settings.BackgroundColor = "#FFFFFF"; // Default background color
			settings.TextColor = "#000000"; // Default text color
		}
		if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
		{
			grid.BackgroundColor = Color.FromArgb(settings.BackgroundColor);
			pageLabel.TextColor = Color.FromArgb(settings.TextColor);
		}
	}

	/// <summary>
	/// Asynchronously closes the menu by performing a series of animations.
	/// </summary>
	/// <remarks>This method triggers the ViewModel's press action and performs fade, scale, and translation
	/// animations on the grid element to close the menu. The animations are executed sequentially with a specified
	/// duration.</remarks>
	/// <param name="sender">The source of the event that triggered the method.</param>
	/// <param name="e">The event data associated with the event.</param>
	async void CloseMenuAsync(object sender, EventArgs e)
	{
		ViewModel.Press();
		await grid.FadeTo(1, animationDuration).ConfigureAwait(false);
		await grid.ScaleTo(1, animationDuration).ConfigureAwait(false);
		await grid.TranslateTo(0, 0, animationDuration, Easing.CubicIn).ConfigureAwait(false);
	}

	void CreateToolBarItem(int index, Chapter chapter)
	{
		ArgumentNullException.ThrowIfNull(book);
		if (string.IsNullOrEmpty(chapter.Title))
		{
			return;
		}
#if IOS || MACCATALYST
		Label label = new()
		{
			Text = chapter.Title,
			TextColor = Colors.White,
			HorizontalOptions = LayoutOptions.End,
			Margin = new Thickness(0, 0, 10, 0),
		};
		label.GestureRecognizers.Add(new TapGestureRecognizer
		{
			Command = new Command(() =>
			{
				Dispatcher.Dispatch(async () =>
				{
					book.CurrentChapter = index;
					db.UpdateBookMark(book);
					pageLabel.Text = await GetCurrentPageInfoAsync();
					var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
					await webView.EvaluateJavaScriptAsync($"loadPage(\"{file}\")");
					CloseMenuAsync(this, EventArgs.Empty);
				});
			})
		});

		menu.Add(label);
		menu.SetRow(label, index);
		menu.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1, GridUnitType.Auto)
		});
#else
		var toolbarItem = new ToolbarItem
		{
			Text = chapter.Title,
			Order = ToolbarItemOrder.Secondary,
			Priority = index,
			Command = new Command(() =>
			{
				Dispatcher.Dispatch(async () =>
				{
					book.CurrentChapter = index;
					db.UpdateBookMark(book);
					pageLabel.Text = await GetCurrentPageInfoAsync();
					var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
					await webView.EvaluateJavaScriptAsync($"loadPage(\"{file}\")");
					CloseMenuAsync(this, EventArgs.Empty);
				});
			})
		};
		Shell.Current.ToolbarItems.Add(toolbarItem);
#endif
	}
}
