using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Util;
using EpubReader.ViewModels;

namespace EpubReader.Views;

public partial class BookPage : ContentPage, IDisposable
{
	Book? book;
	readonly IDb db;
	readonly WebViewHelper webViewHelper;
	readonly WebView webView = new();
	readonly Label pageLabel = new()
	{
		FontSize = 20,
		HorizontalOptions = LayoutOptions.Center,
	};
#if ANDROID || IOS || MACCATALYST
	readonly SwipeGestureRecognizer swipeGestureRecognizer_up = new()
	{
		Direction = SwipeDirection.Up,
	};
#endif
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
	const uint animationDuration = 200u;
	bool disposedValue;
	bool loadIndex = true;
	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		webViewHelper = new(webView);
		webView.Navigated += webView_Navigated;
		webView.Navigating += webView_Navigating;
		webView.Scale = 1;

#pragma warning disable S1075 // URIs should not be hardcoded
#if ANDROID || WINDOWS
		webView.Source = new UrlWebViewSource
		{
			Url = "https://demo/index.html",
		};
#endif
#if IOS || MACCATALYST
		webView.Source = new UrlWebViewSource
		{
			Url = "app://demo/index.html",
		};
		swipeGestureRecognizer_left.Swiped += SwipeGestureRecognizer_left_Swiped;
		swipeGestureRecognizer_right.Swiped += SwipeGestureRecognizer_right_Swiped;
		webView.GestureRecognizers.Add(swipeGestureRecognizer_left);
		webView.GestureRecognizers.Add(swipeGestureRecognizer_right);
		webView.GestureRecognizers.Add(swipeGestureRecognizer_up);
#endif
#pragma warning restore S1075 // URIs should not be hardcoded
#if IOS
		webView.Behaviors.Add(touchbehavior);
#elif ANDROID
		webView.GestureRecognizers.Add(swipeGestureRecognizer_up);
		webView.Behaviors.Add(touchbehavior);
#endif
#if IOS || MACCATALYST || ANDROID
		swipeGestureRecognizer_up.Swiped += SwipeGestureRecognizer_up_Swiped;
#endif
		grid.SetRow(pageLabel, 1);
		grid.Children.Add(webView);
		grid.Children.Add(pageLabel);
	}

	async void SwipeGestureRecognizer_left_Swiped(object? sender, SwipedEventArgs e)
	{
		if (e.Direction == SwipeDirection.Left)
		{
			await webView.EvaluateJavaScriptAsync(" window.parent.postMessage(\"next\", \"app://demo\");");
		}
	}

	void SwipeGestureRecognizer_up_Swiped(object? sender, SwipedEventArgs e)
	{
		if (e.Direction == SwipeDirection.Up)
		{
			GridArea_Tapped(this, EventArgs.Empty);
		}
	}
	async void SwipeGestureRecognizer_right_Swiped(object? sender, SwipedEventArgs e)
	{
		if (e.Direction == SwipeDirection.Right)
		{
			await webView.EvaluateJavaScriptAsync("window.parent.postMessage(\"prev\", \"app://demo\");");
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
		if (!loadIndex)
		{
			return;
		}
		loadIndex = false;
		await webViewHelper.LoadPage(pageLabel, book);
		Shimmer.IsActive = false;
	}

	async void webView_Navigating(object? sender, WebNavigatingEventArgs e)
	{
		var urlParts = e.Url.Split('.');
		ArgumentNullException.ThrowIfNull(book);
		if (urlParts[0].Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			e.Cancel = true;
			var funcToCall = urlParts[1].Split("?");
			var methodName = funcToCall[0][..^1];
			if (methodName.Contains("next", StringComparison.CurrentCultureIgnoreCase))
			{
				await webViewHelper.Next(pageLabel, book);
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				await webViewHelper.Prev(pageLabel, book);
			}
			if (methodName.Contains("menu", StringComparison.CurrentCultureIgnoreCase))
			{
				GridArea_Tapped(this, EventArgs.Empty);
			}
			if (methodName.Contains("pageLoad", StringComparison.CurrentCultureIgnoreCase))
			{
				await webViewHelper.OnSettingsClicked();
			}
		}
	}

	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		pageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		var bytes = book.CoverImage;
		image.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, (r, m) => webViewHelper.OnJavaScriptMessageReceived(m, pageLabel, book));
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => await webViewHelper.OnSettingsClicked());
	}

	async void GridArea_Tapped(object sender, EventArgs e)
	{
		var viewModel = (BookViewModel)BindingContext;
		viewModel.Press();
		//Open the menu and hide the main content
		// Reveal our menu and move the main content out of the view
		var width = this.Width * 0.4;
		if(OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
		{
			width = this.Width * 0.8;
		}
		await grid.TranslateTo(-width, this.Height * 0.1, animationDuration, Easing.CubicIn).ConfigureAwait(false);
		await grid.ScaleTo(0.8, animationDuration).ConfigureAwait(false);
		await grid.FadeTo(0.8, animationDuration).ConfigureAwait(false);
	}

	async void CloseMenu(object sender, EventArgs e)
	{
		var viewModel = (BookViewModel)BindingContext;
		viewModel.Press();
		//Close the menu and bring back back the main content
		await grid.FadeTo(1, animationDuration).ConfigureAwait(false);
		await grid.ScaleTo(1, animationDuration).ConfigureAwait(false);
		await grid.TranslateTo(0, 0, animationDuration, Easing.CubicIn).ConfigureAwait(false);
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
					pageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
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
					pageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
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
