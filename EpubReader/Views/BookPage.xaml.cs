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
#if WINDOWS
	bool loadIndex = true;
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
	readonly SwipeGestureRecognizer swipeGestureRecognizer_up = new()
	{
		Direction = SwipeDirection.Up,
	};
#endif
	const uint animationDuration = 200u;
	bool disposedValue;
	
	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		webViewHelper = new(webView);

#if IOS || MACCATALYST
		swipeGestureRecognizer_left.Swiped += SwipeGestureRecognizer_left_Swiped;
		swipeGestureRecognizer_right.Swiped += SwipeGestureRecognizer_right_Swiped;
		swipeGestureRecognizer_up.Swiped += SwipeGestureRecognizer_up_Swiped;
		webView.GestureRecognizers.Add(swipeGestureRecognizer_left);
		webView.GestureRecognizers.Add(swipeGestureRecognizer_right);
		webView.GestureRecognizers.Add(swipeGestureRecognizer_up);
#endif
#if IOS || ANDROID
		webView.Behaviors.Add(touchbehavior);
#endif
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

#if WINDOWS
	protected override bool OnBackButtonPressed()
	{
		WeakReferenceMessenger.Default.UnregisterAll(this);
		Shell.Current.ToolbarItems.Clear();
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
		return base.OnBackButtonPressed();
	}
#endif

	protected override void OnDisappearing()
	{
#if WINDOWS
		loadIndex = false;
#endif
#if ANDROID
		System.Diagnostics.Debug.WriteLine("OnDisappearing called");
		WeakReferenceMessenger.Default.UnregisterAll(this);
		Shell.Current.ToolbarItems.Clear();
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
#endif
		base.OnDisappearing();
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
#if WINDOWS
		if (!loadIndex && !e.Url.Contains("https://demo/index.html"))
		{
			return;
		}
		loadIndex = false;
#endif
		await webViewHelper.LoadPage(pageLabel, book);
		Shimmer.IsActive = false;
	}

	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		pageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
		{
			WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, (r, m) => { webView_Navigating(this, new WebNavigatingEventArgs(WebNavigationEvent.NewPage, null, m.Value)); });
		}

		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => { await webViewHelper.OnSettingsClicked(); UpdateUiAppearance(); });
	}

	async void webView_Navigating(object? sender, WebNavigatingEventArgs e)
	{
		if (!e.Url.Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			return;
		}

		e.Cancel = true;
		ArgumentNullException.ThrowIfNull(book);

		if (await TryHandleExternalLinkAsync(e.Url))
		{
			return;
		}

		var methodName = GetMethodNameFromUrl(e.Url);
		if (string.IsNullOrEmpty(methodName))
		{
			return;
		}

		await HandleWebViewActionAsync(methodName, e.Url);
		UpdateUiAppearance();
	}

	static async Task<bool> TryHandleExternalLinkAsync(string url)
	{
		const string externalLinkPrefix = "https://runcsharp.jump/?";
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

	async Task HandleWebViewActionAsync(string methodName, string url)
	{
		ArgumentNullException.ThrowIfNull(book);
		switch (methodName.ToLowerInvariant())
		{
			case "jump":
				HandleJumpAction(url);
				break;
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
				break;
		}
	}

	void HandleJumpAction(string url)
	{
		ArgumentNullException.ThrowIfNull(book);
		var urlParts = url.Split("https://demo/");
		if (urlParts.Length <= 1)
		{
			return;
		}

		var key = urlParts[1].Split('#')[0];
		var index = book.Chapters.FindIndex(chapter => chapter.FileName.Contains(key, StringComparison.CurrentCultureIgnoreCase));

		if (index >= 0)
		{
			book.CurrentChapter = index;
			db.UpdateBookMark(book);
			pageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		}
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
