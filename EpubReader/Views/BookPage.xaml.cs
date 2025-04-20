using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Util;
using EpubReader.ViewModels;
using Microsoft.Maui.Handlers;


namespace EpubReader.Views;

public partial class BookPage : ContentPage, IDisposable
{
	bool loadIndex = true;
#if ANDROID || IOS || MACCATALYST
	readonly CommunityToolkit.Maui.Behaviors.TouchBehavior touchbehavior = new();
#endif
	readonly IDb db;
	Book? book;
	bool disposedValue;

	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		System.Diagnostics.Trace.WriteLine($"BookPage: {book.Title}");
	}

	protected override void OnDisappearing()
	{
		if (BindingContext is BookViewModel viewModel)
		{
			viewModel.Dispose();
		}
#if WINDOWS
		WebViewExtensions.WebView2_Unloaded();
#endif
		base.OnDisappearing();
	}

	async void webView_Navigated(object sender, WebNavigatedEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(book);
		System.Diagnostics.Trace.WriteLine($"webView_Navigated: {e.Url}");
		if (!loadIndex)
		{
			return;
		}
		loadIndex = false;
		await WebViewExtensions.LoadPage(PageLabel, EpubText, book);
		Shimmer.IsActive = false;
	}

	async void webView_Navigating(object sender, WebNavigatingEventArgs e)
	{
		var urlParts = e.Url.Split('.');
		System.Diagnostics.Trace.WriteLine($"webView_Navigating: {e.Url}");
		ArgumentNullException.ThrowIfNull(book);
		if (urlParts[0].Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			e.Cancel = true;
			var funcToCall = urlParts[1].Split("?");
			var methodName = funcToCall[0][..^1];
			if (methodName.Contains("next", StringComparison.CurrentCultureIgnoreCase))
			{
				await WebViewExtensions.Next(PageLabel, EpubText, book);
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				await WebViewExtensions.Prev(PageLabel, book, EpubText);
			}
			if (methodName.Contains("pageLoad", StringComparison.CurrentCultureIgnoreCase))
			{
				var webViewHandler = EpubText.Handler as IWebViewHandler ?? throw new InvalidOperationException("WebViewHandler is null");
				await WebViewExtensions.OnSettingsClicked(webViewHandler);
			}
		}
	}
	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		System.Diagnostics.Trace.WriteLine($"CurrentPage_Loaded: {EpubText.Source}");
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		var webViewHandler = EpubText.Handler as IWebViewHandler ?? throw new InvalidOperationException("WebViewHandler is null");
#if ANDROID || IOS || MACCATALYST
		EpubText.Behaviors.Add(touchbehavior);
		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, (r, m) => WebViewExtensions.OnJavaScriptMessageReceived(m, PageLabel, book, EpubText));
#endif
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => await WebViewExtensions.OnSettingsClicked(webViewHandler));
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
	}
	

	void CreateToolBarItem(int index, Chapter chapter)
	{
		ArgumentNullException.ThrowIfNull(book);
		if (string.IsNullOrEmpty(chapter.Title))
		{
			return;
		}
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
					PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
					var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
					await EpubText.EvaluateJavaScriptAsync($"loadPage(\"{file}\")");
				});
			})
		};
		Shell.Current.ToolbarItems.Add(toolbarItem);
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);

		WeakReferenceMessenger.Default.UnregisterAll(this);
		Shell.Current.ToolbarItems.Clear();
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
	}

	public void SwipeGestureRecognizer_Swiped(object? sender, SwipedEventArgs e)
	{
		if (sender is null)
		{
			return;
		}
		if (e.Direction == SwipeDirection.Up)
		{
			var viewModel = (BookViewModel)BindingContext;
			viewModel.Press();
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
#if ANDROID || IOS || MACCATALYST
				touchbehavior.Dispose();
#endif
			}
			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
