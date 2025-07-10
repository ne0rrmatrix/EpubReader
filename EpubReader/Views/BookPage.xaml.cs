using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Util;
using EpubReader.ViewModels;

namespace EpubReader.Views;

public partial class BookPage : ContentPage
{
	const string externalLinkPrefix = "https://runcsharp.jump/?";
	const uint animationDuration = 200u;

	Book? book;
	readonly IDb db;
	readonly WebViewHelper webViewHelper;

	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		webViewHelper = new(webView);

		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, async (r, m) => await HandleJavascript(m.Value));
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => { await webViewHelper.OnSettingsClicked(); UpdateUiAppearance(); });

	}

	protected override void OnDisappearing()
	{
		var viewModel = (BookViewModel)BindingContext;
		if (!viewModel.isPopupActive)
		{
			WeakReferenceMessenger.Default.UnregisterAll(this);
			Shell.Current.ToolbarItems.Clear();
			Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
		}
		base.OnDisappearing();
	}

	async void GridArea_Tapped(object sender, EventArgs e)
	{
		var viewModel = (BookViewModel)BindingContext;
		viewModel.Press();
		var width = this.Width * 0.4;
		if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
		{
			width = this.Width * 0.8;
		}
		await grid.TranslateTo(-width, this.Height * 0.1, animationDuration, Easing.CubicIn).ConfigureAwait(false);
		await grid.ScaleTo(0.8, animationDuration).ConfigureAwait(false);
		await grid.FadeTo(0.8, animationDuration).ConfigureAwait(false);
	}

	async void webView_Navigated(object? sender, WebNavigatedEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(book);
		await webViewHelper.LoadPage(pageLabel, book);
		Shimmer.IsActive = false;
	}

	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
	}
	async Task HandleJavascript(string url)
	{
		ArgumentNullException.ThrowIfNull(book);
		if (await TryHandleExternalLinkAsync(url))
		{
			var link = url.Replace(externalLinkPrefix, string.Empty);
			var temp = book.Chapters.Find(chapter => chapter.FileName.Contains(Path.GetFileName(link), StringComparison.OrdinalIgnoreCase));
			if (temp is not null)
			{
				book.CurrentChapter = book.Chapters.IndexOf(temp);
				db.UpdateBookMark(book);
			}
			return;
		}

		var methodName = GetMethodNameFromUrl(url);
		if (string.IsNullOrEmpty(methodName))
		{
			return;
		}

		await HandleWebViewActionAsync(methodName);
		UpdateUiAppearance();
	}
	async void webView_Navigating(object? sender, WebNavigatingEventArgs e)
	{
		var url = e.Url;
		ArgumentNullException.ThrowIfNull(book);
		
		if (!url.Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			return;
		}
		
		
		e.Cancel = true;
		await HandleJavascript(e.Url);
	}

	static async Task<bool> TryHandleExternalLinkAsync(string url)
	{
		if (!url.Contains(externalLinkPrefix, StringComparison.OrdinalIgnoreCase) || url.Contains("https://demo"))
		{
			return false;
		}

		var urlParts = url.Split('?');
		if (urlParts.Length <= 1)
		{
			return false;
		}

		var queryString = urlParts[1].Replace("http://", "https://");
		if (!string.IsNullOrEmpty(queryString) && queryString.Contains("https://"))
		{
			await Launcher.OpenAsync(queryString);
			return true;
		}

		return false;
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
		ArgumentNullException.ThrowIfNull(book);
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
				await webViewHelper.OnSettingsClicked();
				UpdateUiAppearance();
				pageLabel.Text = await GetCurrentPageInfo();
				break;
			case "updatepageinfo":
				pageLabel.Text = await GetCurrentPageInfo();
				break;
		}
	}

	async Task<string> GetCurrentPageInfo()
	{
		ArgumentNullException.ThrowIfNull(book);
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

	async void CloseMenu(object sender, EventArgs e)
	{
		var viewModel = (BookViewModel)BindingContext;
		viewModel.Press();
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
					pageLabel.Text = await GetCurrentPageInfo();
					var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
					await webView.EvaluateJavaScriptAsync($"loadPage(\"{file}\")");
					CloseMenu(this, EventArgs.Empty);
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
					pageLabel.Text = await GetCurrentPageInfo();
					var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
					await webView.EvaluateJavaScriptAsync($"loadPage(\"{file}\")");
					CloseMenu(this, EventArgs.Empty);
				});
			})
		};
		Shell.Current.ToolbarItems.Add(toolbarItem);
#endif
	}
}
