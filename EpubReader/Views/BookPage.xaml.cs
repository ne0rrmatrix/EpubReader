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
	
#if ANDROID || IOS
	readonly CommunityToolkit.Maui.Behaviors.TouchBehavior touchbehavior = new();
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

#if IOS || ANDROID
		webView.Behaviors.Add(touchbehavior);
#endif
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
		Controls.WebViewExtensions.WebView2_Unloaded();
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
		if (!urlParts[0].Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			return;	
		}
		e.Cancel = true;
		var funcToCall = urlParts[1].Split("?");
		var methodName = funcToCall[0][..^1];
		var newUrl = e.Url.Split('?');
		if (newUrl.Length > 1 && e.Url.Contains("https://runcsharp.jump/?") && !e.Url.Contains("https://demo"))
		{
			var queryString = newUrl[1];
			queryString = queryString.Replace("http://", "https://");
			if (string.IsNullOrEmpty(queryString) || !queryString.Contains("https://"))
			{
				return;
			}
			await Launcher.OpenAsync(queryString);
			return;
		}

		string[] url = e.Url.Split("https://demo/");
		
		if (url.Length > 1 && 
			methodName.Contains("jump", StringComparison.CurrentCultureIgnoreCase))
		{
			var index = book.Chapters.FindIndex(chapter => chapter.FileName.Contains(url[1].Split('#')[0], StringComparison.CurrentCultureIgnoreCase));
			if (index < 0)
			{
				return;
			}
			book.CurrentChapter = index;
			db.UpdateBookMark(book);
			pageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		}
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

	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		pageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		if(OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
		{
			WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, (r, m) => { webView_Navigating(this, new WebNavigatingEventArgs(WebNavigationEvent.NewPage, null, m.Value)); });
		}
		
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => await webViewHelper.OnSettingsClicked());
	}

	async void GridArea_Tapped(object sender, EventArgs e)
	{
		var viewModel = (BookViewModel)BindingContext;
		viewModel.Press();
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
