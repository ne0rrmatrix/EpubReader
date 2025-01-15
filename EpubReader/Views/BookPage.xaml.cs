#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
#endif

using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.ViewModels;

namespace EpubReader.Views;

public partial class BookPage : ContentPage
{
	readonly IDb db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	bool isPreviousPage = false;
	Book book = new();
	Settings settings = new();
	public BookPage(BookViewModel viewModel)
    {
        InitializeComponent();
		BindingContext = viewModel;
		
		System.Diagnostics.Debug.WriteLine("BookPage constructor");
		
		EpubText.Navigating += EpubText_Navigating;
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());
		EpubText.Navigated += OnEpubText_Navigated;

#if ANDROID
		StatusBarExtensions.SetStatusBarsHidden(true);
#endif
	}

	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException();
		settings = ((BookViewModel)BindingContext).Settings ?? throw new InvalidOperationException();
		CreateToolBar(book);
	}

	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(false);
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; });
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
				var html = InjectIntoHtml.InjectAllCss(chapter.HtmlFile, book, settings);
				Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; });
			})
		};
		Dispatcher.Dispatch(() => Shell.Current.ToolbarItems.Add(toolbarItem));
	}

	void CreateToolBar(Book book)
	{
		var chapters = book.Chapters;

		for (var i = 0; i < chapters.Count; i++)
		{
			CreateToolBarItem(i, chapters[i]);
		}
	}
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        EpubText.Navigating -= EpubText_Navigating;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        EpubText.Navigated -= OnEpubText_Navigated;

        Shell.Current.ToolbarItems.Clear();
        Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
#if ANDROID
        StatusBarExtensions.SetStatusBarsHidden(false);
#endif
    }

	async void OnEpubText_Navigated(object? sender, WebNavigatedEventArgs e)
	{
		System.Diagnostics.Debug.WriteLine("OnEpubText_Navigated");
		if (isPreviousPage)
		{
			System.Diagnostics.Debug.WriteLine("Previous page");
			isPreviousPage = false;
			
			Dispatcher.Dispatch(() => EpubText.Eval("window.scrollTo(0, document.body.scrollHeight)"));
			await SetLabelText();
			return;
		}
		if(!book.HasPages && book.CurrentPage > 0)
		{
			System.Diagnostics.Debug.WriteLine("No Page index");
			System.Diagnostics.Debug.WriteLine($"Loading page: {book.CurrentPage}");
			Dispatcher.Dispatch(() => EpubText.Eval($"window.scrollTo(0, {book.CurrentPage})"));
			await SetLabelText();
			return;
		}
		if (book.CurrentPage > 0)
		{
			System.Diagnostics.Debug.WriteLine("Page index");
			System.Diagnostics.Debug.WriteLine($"Loading page: {book.CurrentPage}");
			Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter].Title} - Page {book.CurrentPage}");
			Dispatcher.Dispatch(() => EpubText.Eval($"scrollToPage({book.CurrentPage})"));
			await SetLabelText();
		}
		else
		{
			System.Diagnostics.Debug.WriteLine("No page index or scroll position found!");
			Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter].Title}");
			await SetLabelText();
		}
	}

	static void EpubText_Navigating(object? sender, WebNavigatingEventArgs e)
	{
		System.Diagnostics.Debug.WriteLine("EpubText_Navigating");
		if (e.Url.Contains("http://") || e.Url.Contains("https://") || e.Url.Contains("file:"))
		{
			e.Cancel = true;
		}
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
			Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; });
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
			if (book.CurrentChapter < 0 || book.CurrentChapter >= book.Chapters.Count - 1)
			{
				return;
			}
			book.CurrentChapter++;
			await db.SaveBookData(book, CancellationToken.None);
			var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
			Dispatcher.Dispatch(() => EpubText.Source = new HtmlWebViewSource { Html = html });
			return;
		}
		EpubText.Eval("window.scrollBy(0, window.innerHeight)");
		await SetLabelText();
	}


	async Task SetLabelText()
	{
		string current = string.Empty;
		if (!book.HasPages)
		{
			current = await EpubText.EvaluateJavaScriptAsync("getVerticalScroll()") ?? string.Empty;
			if (!string.IsNullOrEmpty(current) && double.TryParse(current, out double result))
			{
				System.Diagnostics.Debug.WriteLine($"Current page scroll position: {result}");
				book.CurrentPage = (int)result;
				await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
				await Dispatcher.DispatchAsync(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter].Title}");
				return;
			}
		}
		current = await EpubText.EvaluateJavaScriptAsync("getCurrentPage()") ?? string.Empty;
		if (!current.Contains("null") && !string.IsNullOrEmpty(current) && !current.Equals("0"))
		{
			book.CurrentPage = Int32.Parse(current);
			System.Diagnostics.Debug.WriteLine($"Current page: {book.CurrentPage}");
			await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
			Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter].Title} - Page {current}");
			return;
		}
		
		Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter].Title}");
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
