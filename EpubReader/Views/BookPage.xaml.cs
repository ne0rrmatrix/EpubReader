#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Core.Platform;
#endif

using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class BookPage : ContentPage
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
	readonly IDb db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	Book book = new();
    int currentChapterIndex = 0;
	bool isPreviousPage = false;
	Settings settings = new();
	public BookPage(BookViewModel viewModel)
    {
        InitializeComponent();
		BindingContext = viewModel;
		EpubText.Navigating += EpubText_Navigating;
		SettingsPageHelpers.SettingsPropertyChanged += OnSettingsClicked;
		EpubText.Navigated += OnEpubText_Navigating;
#if ANDROID
		StatusBarExtensions.SetStatusBarsHidden(true);
#endif
	}

	async void OnEpubText_Navigating(object? sender, WebNavigatedEventArgs e)
	{
		if (isPreviousPage)
		{
			isPreviousPage = false;
			EpubText.Eval("window.scrollTo(0, document.body.scrollHeight)");
			await SetLabelText();
			return;
		}
		await SetLabelText();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
	}
	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		SettingsPageHelpers.SettingsPropertyChanged -= OnSettingsClicked;
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
#if ANDROID
		StatusBarExtensions.SetStatusBarsHidden(false);
#endif
	}

	void EpubText_Navigating(object? sender, WebNavigatingEventArgs e)
	{
		if (e.Url.Contains("http://") || e.Url.Contains("https://") || e.Url.Contains("file:"))
		{
			e.Cancel = true;
		}
	}

	async void OnSettingsClicked(object? sender, EventArgs e)
	{
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(true);
		var html = GetHtmlWithCss(book.Chapters[currentChapterIndex].HtmlFile);
		Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; });
	}
	
	async void ContentPage_Loaded(object sender, EventArgs e)
    {
        book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException($"Invalid Operation: {book}");
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(true);
		CreateToolBar(book);
        var html = GetHtmlWithCss(book.Chapters[0].HtmlFile);
        Dispatcher.Dispatch(() => { EpubText.MaximumHeightRequest = Height - 30; EpubText.Source = new HtmlWebViewSource { Html = html }; });
    }

	void CreateToolBar(Book book)
    {
        Shell.Current.ToolbarItems.Clear();
        var chapters = book.Chapters;

        for (var i = 0; i < chapters.Count; i++)
        {
            CreateToolBarItem(i, chapters[i]);
        }
    }

    void CreateToolBarItem(int index, Chapter chapter)
    {
        var toolbarItem = new ToolbarItem
        {
            Text = chapter.Title,
            Order = ToolbarItemOrder.Secondary,
            Priority = index,
            Command = new Command(() =>
            {
				var html = GetHtmlWithCss(chapter.HtmlFile);
				EpubText.Source = new HtmlWebViewSource { Html = html };
            })
        };
        MainThread.BeginInvokeOnMainThread(() => Shell.Current.ToolbarItems.Add(toolbarItem));
    }

	async Task PreviousPage()
	{
		var result = await EpubText.EvaluateJavaScriptAsync("ScrolledToTop()");
		if (result is not null && result.Equals("Yes"))
		{
			if (currentChapterIndex <= 0)
			{
				return;
			}
			currentChapterIndex--;
			var html = GetHtmlWithCss(book.Chapters[currentChapterIndex].HtmlFile);
			Dispatcher.Dispatch(() => { isPreviousPage = true; EpubText.Source = new HtmlWebViewSource { Html = html }; });
			return;
		}
		EpubText.Eval("window.scrollBy(0, -window.innerHeight)");
		await SetLabelText();
	}
	
	async void PreviousPage(object sender, EventArgs e)
	{
		await PreviousPage();
	}

	
	async void NextPage(object sender, EventArgs e)
	{
		await NextPage();
	}

	async Task NextPage()
	{
		var result = await EpubText.EvaluateJavaScriptAsync("scrolledToBottom()");
		if (result is not null && result.Equals("Yes"))
		{
			if (currentChapterIndex < 0 || currentChapterIndex >= book.Chapters.Count - 1)
			{
				return;
			}
			currentChapterIndex++;
			var html = GetHtmlWithCss(book.Chapters[currentChapterIndex].HtmlFile);
			Dispatcher.Dispatch(() => EpubText.Source = new HtmlWebViewSource { Html = html });
			return;
		}
		EpubText.Eval("window.scrollBy(0, window.innerHeight)");
		await SetLabelText();
	}

	async Task SetLabelText()
	{
		var current = await EpubText.EvaluateJavaScriptAsync("getCurrentPage()");
		if (current is not null && !current.Contains("null") && !string.IsNullOrEmpty(current) && !current.Equals("0"))
		{
			Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[currentChapterIndex].Title} - Page {current}");
			return;
		}
		Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[currentChapterIndex].Title}");
	}

	string GetHtmlWithCss(string html)
	{
		var css = book.Css[^1].Content ?? string.Empty;
		var cSSInject = new CssInjector(settings, css);
		var temp = cSSInject.InjectAllCss(html, book);
		return temp;
	}

	async void SwipeGestureRecognizer_Swiped(object sender, SwipedEventArgs e)
	{
		if(e.Direction == SwipeDirection.Left)
		{
			await NextPage();
		}
		else if (e.Direction == SwipeDirection.Right)
		{
			await PreviousPage();
		}
	}
}
