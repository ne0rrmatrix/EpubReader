using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Util;
using EpubReader.ViewModels;
using Microsoft.Maui.Handlers;
using Syncfusion.Maui.Toolkit.Carousel;

namespace EpubReader.Views;

public partial class BookPage : ContentPage, IDisposable
{
	readonly SwipeGestureRecognizer swipeGestureRecognizer_up = new()
	{
		Direction = SwipeDirection.Up,
	};

	readonly WebView epubText = new();
	readonly Label pageLabel = new()
	{
		FontSize = 20,
		HorizontalOptions = LayoutOptions.Center,
	};
	bool loadIndex = true;
	readonly IDb db;
	Book? book;
#if ANDROID || IOS
	readonly CommunityToolkit.Maui.Behaviors.TouchBehavior touchbehavior = new();
#endif
#if IOS || MACCATALYST
	readonly SwipeGestureRecognizer swipeGestureRecognizer_left = new()
	{
		Direction = SwipeDirection.Left,
	};
	readonly SwipeGestureRecognizer swipeGestureRecognizer_right = new()
	{
	Direction = SwipeDirection.Right,
	};
#endif
	bool disposedValue;

	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		epubText.Navigated += webView_Navigated;
		epubText.Navigating += webView_Navigating;
		epubText.Scale = 1;
		epubText.Source = viewModel.Source;

#if ANDROID || WINDOWS
		epubText.Source = new UrlWebViewSource
		{
			Url = "https://demo/index.html",
		};
#endif
#if IOS
epubText.Behaviors.Add(touchbehavior);
#endif
#if ANDROID
		swipeGestureRecognizer_up.Swiped += SwipeGestureRecognizer_up_Swiped;
		epubText.GestureRecognizers.Add(swipeGestureRecognizer_up);
		grid.SetRow(epubText, 0);
		epubText.Behaviors.Add(touchbehavior);
#elif IOS || MACCATALYST
		epubText.Source = new UrlWebViewSource
        {
            Url = "app://demo/index.html",
        };
        swipeGestureRecognizer_left.Swiped += SwipeGestureRecognizer_left_Swiped;
        swipeGestureRecognizer_right.Swiped += SwipeGestureRecognizer_right_Swiped;
		swipeGestureRecognizer_up.Swiped += SwipeGestureRecognizer_up_Swiped;
        epubText.GestureRecognizers.Add(swipeGestureRecognizer_left);
        epubText.GestureRecognizers.Add(swipeGestureRecognizer_right);
        epubText.GestureRecognizers.Add(swipeGestureRecognizer_up);
#endif
		grid.SetRow(pageLabel, 1);
		grid.Children.Add(epubText);
		grid.Children.Add(pageLabel);

		
	}
	async void SwipeGestureRecognizer_left_Swiped(object? sender, SwipedEventArgs e)
	{
		if (e.Direction == SwipeDirection.Left)
		{
			System.Diagnostics.Trace.WriteLine("SwipeGesture Right");
			await epubText.EvaluateJavaScriptAsync(" window.parent.postMessage(\"next\", \"app://demo\");");
		}
	}

	void SwipeGestureRecognizer_up_Swiped(object? sender, SwipedEventArgs e)
	{
		System.Diagnostics.Trace.WriteLine($"SwipeGesture: {e.Direction}");
		if (e.Direction == SwipeDirection.Up)
		{
			var viewModel = (BookViewModel)BindingContext;
			viewModel.Press();
		}
	}
	async void SwipeGestureRecognizer_right_Swiped(object? sender, SwipedEventArgs e)
	{
		if (e.Direction == SwipeDirection.Right)
		{
			System.Diagnostics.Trace.WriteLine("SwipeGesture Left");
			await epubText.EvaluateJavaScriptAsync("window.parent.postMessage(\"prev\", \"app://demo\");");
		}
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

	async void webView_Navigated(object? sender, WebNavigatedEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(book);
		System.Diagnostics.Trace.WriteLine($"webView_Navigated: {e.Url}");
		if (!loadIndex)
		{
			return;
		}
		loadIndex = false;
		await WebViewExtensions.LoadPage(pageLabel, epubText, book);
		Shimmer.IsActive = false;
	}

	async void webView_Navigating(object? sender, WebNavigatingEventArgs e)
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
				await WebViewExtensions.Next(pageLabel, epubText, book);
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				await WebViewExtensions.Prev(pageLabel, book, epubText);
			}
			if (methodName.Contains("pageLoad", StringComparison.CurrentCultureIgnoreCase))
			{
				var webViewHandler = epubText.Handler as IWebViewHandler ?? throw new InvalidOperationException("WebViewHandler is null");
				await WebViewExtensions.OnSettingsClicked(webViewHandler);
			}
		}
	}
	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		System.Diagnostics.Trace.WriteLine($"CurrentPage_Loaded: {epubText.Source}");
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		pageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
	
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
#if ANDROID
		var binding = (BookViewModel)BindingContext;
		pageLabel.SetBinding(Label.IsVisibleProperty, nameof(binding.IsNavMenuVisible));
#endif
		var webViewHandler = epubText.Handler as IWebViewHandler ?? throw new InvalidOperationException("WebViewHandler is null");
		var viewModel = (BookViewModel)BindingContext;
		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(viewModel, (r, m) => WebViewExtensions.OnJavaScriptMessageReceived(m, pageLabel, viewModel.Book, epubText));
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => await WebViewExtensions.OnSettingsClicked(webViewHandler));
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
					pageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
					var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
					await epubText.EvaluateJavaScriptAsync($"loadPage(\"{file}\")");
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

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
#if ANDROID || IOS
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
