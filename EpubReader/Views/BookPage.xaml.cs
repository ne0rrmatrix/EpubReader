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
	BookViewModel ViewModel => (BookViewModel)BindingContext;
	Book book => ViewModel.Book;
	readonly IDb db;
	readonly WebViewHelper webViewHelper;

	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		webViewHelper = new(webView);

		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, async (r, m) => await HandleJavascriptAsync(m.Value));
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => { await webViewHelper.OnSettingsClickedAsync(); UpdateUiAppearance(); });

	}

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

	async void webView_Navigated(object? sender, WebNavigatedEventArgs e)
	{
		await webViewHelper.LoadPage(pageLabel, book);
		Shimmer.IsActive = false;
	}

	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
	}

	async Task HandleJavascriptAsync(string url)
	{
		await TryHandleInternalLinkAsync(url);
		await TryHandleExternalLinkAsync(url);
		var methodName = GetMethodNameFromUrl(url);
		await HandleWebViewActionAsync(methodName);
		UpdateUiAppearance();
	}
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
		if (!url.Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
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
	async Task TryHandleExternalLinkAsync(string url)
	{
		if (!url.Contains(externalLinkPrefix, StringComparison.OrdinalIgnoreCase) || url.Contains("https://demo"))
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

			var link = url.Replace(externalLinkPrefix, string.Empty);
			var chapter = book.Chapters.Find(chapter => chapter.FileName.Contains(Path.GetFileName(link), StringComparison.OrdinalIgnoreCase));
			if (chapter is null)
			{
				return;
			}
			book.CurrentChapter = book.Chapters.IndexOf(chapter);
			db.UpdateBookMark(book);
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
