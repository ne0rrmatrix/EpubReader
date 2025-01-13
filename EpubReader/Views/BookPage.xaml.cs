#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
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
	bool isPreviousPage = false;
	Book book = new();
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

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(false);
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException();
	}
	async void OnEpubText_Navigating(object? sender, WebNavigatedEventArgs e)
	{
		if (isPreviousPage)
		{
			isPreviousPage = false;
			EpubText.Eval("window.scrollTo(0, document.body.scrollHeight)");
			await SetLabelText().ConfigureAwait(false);
			return;
		}
		if (book.CurrentPage > 0)
		{
			EpubText.Eval($"scrollToPage({book.CurrentPage})");
		}

		await Dispatcher.DispatchAsync(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter].Title} - Page {book.CurrentPage}").ConfigureAwait(false);
	}
	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(false);
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException();
		await CreateToolBar(book).ConfigureAwait(false);
	}

	async Task CreateToolBarItem(int index, Chapter chapter)
	{
		var toolbarItem = new ToolbarItem
		{
			Text = chapter.Title,
			Order = ToolbarItemOrder.Secondary,
			Priority = index,
			Command = new Command(() =>
			{
				var html = InjectIntoHtml.InjectAllCss(chapter.HtmlFile, book, settings);
				Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; });
			})
		};
		await Dispatcher.DispatchAsync(() => Shell.Current.ToolbarItems.Add(toolbarItem)).ConfigureAwait(false);
	}

	async Task CreateToolBar(Book book)
	{
		var chapters = book.Chapters;

		for (var i = 0; i < chapters.Count; i++)
		{
			await CreateToolBarItem(i, chapters[i]).ConfigureAwait(false);
		}
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
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		await Dispatcher.DispatchAsync(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; }).ConfigureAwait(false);
	}

	async Task PreviousPage()
	{
		var result = await EpubText.EvaluateJavaScriptAsync("ScrolledToTop()");
		if (result is not null && result.Equals("Yes"))
		{
			if (book.CurrentChapter <= 0)
			{
				return;
			}
			book.CurrentChapter--;
			await db.SaveBookData(book, CancellationToken.None);
			var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
			await Dispatcher.DispatchAsync(() => { isPreviousPage = true; EpubText.Source = new HtmlWebViewSource { Html = html }; }).ConfigureAwait(false);
			return;
		}
		EpubText.Eval("window.scrollBy(0, -window.innerHeight)");
		await SetLabelText().ConfigureAwait(false);
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
			if (book.CurrentChapter < 0 || book.CurrentChapter >= book.Chapters.Count - 1)
			{
				return;
			}
			book.CurrentChapter++;
			await db.SaveBookData(book, CancellationToken.None);
			var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
			await Dispatcher.DispatchAsync(() => EpubText.Source = new HtmlWebViewSource { Html = html }).ConfigureAwait(false);
			return;
		}
		EpubText.Eval("window.scrollBy(0, window.innerHeight)");
		await SetLabelText().ConfigureAwait(false);
	}

	async Task SetLabelText()
	{
		var current = await EpubText.EvaluateJavaScriptAsync("getCurrentPage()");
		if (current is not null && !current.Contains("null") && !string.IsNullOrEmpty(current) && !current.Equals("0"))
		{
			book.CurrentPage = Int32.Parse(current);
			await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
			await Dispatcher.DispatchAsync(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter].Title} - Page {current}");
			return;
		}
		await Dispatcher.DispatchAsync(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter].Title}");
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
