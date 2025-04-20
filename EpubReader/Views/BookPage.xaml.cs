using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Util;
using EpubReader.ViewModels;
using Microsoft.Maui.Handlers;

#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
#endif

namespace EpubReader.Views;

public partial class BookPage : ContentPage, IDisposable
{
	bool loadIndex = true;
#if ANDROID
	readonly CommunityToolkit.Maui.Behaviors.TouchBehavior touchbehavior = new();
#endif
	readonly IDb db;
	Book book = new();
	bool disposedValue;

	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
#if ANDROID
		EpubText.Behaviors.Add(touchbehavior);
		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, (r, m) => OnJavaScriptMessageReceived(m));
#endif
	}
#if ANDROID
	async void OnJavaScriptMessageReceived(JavaScriptMessage m)
	{
		if(m.Value.Contains("next", StringComparison.CurrentCultureIgnoreCase))
		{
			await Next();
			return;
		}
		if (m.Value.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
		{
			await Prev();
		}
		if (m.Value.Contains("pageLoad", StringComparison.CurrentCultureIgnoreCase))
		{
			var webViewHandler = EpubText.Handler as IWebViewHandler ?? throw new InvalidOperationException("WebViewHandler is null");
			await WebViewExtensions.OnSettingsClicked(webViewHandler);
		}
	}
#endif

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
	
	async Task Next()
	{
		if(book.CurrentChapter < book.Chapters.Count - 1)
		{
			book.CurrentChapter++;
			db.UpdateBookMark(book);
			await LoadPage();
		}
	}

	async Task Prev()
	{
		if (book.CurrentChapter > 0)
		{
			book.CurrentChapter--;
			db.UpdateBookMark(book);
			await EpubText.EvaluateJavaScriptAsync("setPreviousPage()");
			await LoadPage();
		}
	}
	
	async Task LoadPage()
	{
		var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
		await EpubText.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
	}
	async void webView_Navigated(object sender, WebNavigatedEventArgs e)
	{
		if (!loadIndex)
		{
			return;
		}
		loadIndex = false;
		await LoadPage();
		Shimmer.IsActive = false;
	}

	async void webView_Navigating(object sender, WebNavigatingEventArgs e)
	{
		var urlParts = e.Url.Split('.');
		if (urlParts[0].Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			e.Cancel = true;
			var funcToCall = urlParts[1].Split("?");
			var methodName = funcToCall[0][..^1];
			if (methodName.Contains("next", StringComparison.CurrentCultureIgnoreCase))
			{
				await Next();
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				await Prev();
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
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		var webViewHandler = EpubText.Handler as IWebViewHandler ?? throw new InvalidOperationException("WebViewHandler is null");
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => await WebViewExtensions.OnSettingsClicked(webViewHandler));
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
	}
	

	void CreateToolBarItem(int index, Chapter chapter)
	{
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
#if ANDROID
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
