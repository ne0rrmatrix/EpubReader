#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
#endif

using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.ViewModels;
using MetroLog;
using Microsoft.Maui.Controls;
using Syncfusion.Maui.Toolkit.Themes;

namespace EpubReader.Views;

public partial class BookPage : ContentPage, IDisposable
{
#if ANDROID
	readonly CommunityToolkit.Maui.Behaviors.TouchBehavior touchbehavior = new();
#endif
	bool isPreviousPage = false;
	readonly IDb db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
	Book book = new();
	Settings settings = new();
	bool disposedValue;

	public BookPage(BookViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
#if ANDROID
		var temp = (BookViewModel)BindingContext;
		EpubText.Behaviors.Add(touchbehavior);
#endif
		Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
	}

	void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		Dispatcher.Dispatch(() => UpdateTheme());
	}

	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book;
		settings = ((BookViewModel)BindingContext).Settings;

		EpubText.Navigating += EpubText_Navigating;
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());
		if (!OperatingSystem.IsAndroid())
		{
			EpubText.Navigated += OnEpubText_Navigated;
		}

		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		Dispatcher.Dispatch(async () => await UpdateWebView());
	}

	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(false);
		Dispatcher.Dispatch(async () => await UpdateWebView());
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
				Dispatcher.Dispatch(async () =>
				{
					EpubText.Source = new HtmlWebViewSource { Html = html };
					book.CurrentChapter = index;
					PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
					await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
				});
			})
		};
		Shell.Current.ToolbarItems.Add(toolbarItem);
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		ArgumentNullException.ThrowIfNull(Application.Current);

		EpubText.Navigating -= EpubText_Navigating;
		EpubText.Navigated -= OnEpubText_Navigated;
		Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;

		WeakReferenceMessenger.Default.UnregisterAll(this);
		Shell.Current.ToolbarItems.Clear();
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
	}

	void OnEpubText_Navigated(object? sender, WebNavigatedEventArgs e)
	{
		Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}");
	}

	static void EpubText_Navigating(object? sender, WebNavigatingEventArgs e)
	{
		if (e.Url.Contains("http://") || e.Url.Contains("https://") || e.Url.Contains("file:"))
		{
			e.Cancel = true;
		}
	}

	async Task PreviousPage()
	{
		if (book.CurrentChapter <= 0)
		{
			logger.Info("Start of book");
			return;
		}
		var result = await EpubText.EvaluateJavaScriptAsync("isHorizontalScrollAtStart()");
		if (result.Equals("true"))
		{
			book.CurrentChapter--;
			await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
			isPreviousPage = true;
			Dispatcher.Dispatch(async () => await UpdateWebView());
		
			return;
		}
		EpubText.Eval("prevPage()");
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
		if (book.CurrentChapter >= book.Chapters.Count)
		{
			logger.Info("End of book");
			return;
		}
		var result = await EpubText.EvaluateJavaScriptAsync("isHorizontallyScrolledToEnd()");
		if (result.Equals("true"))
		{
			book.CurrentChapter++;
			await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
			Dispatcher.Dispatch(async () => await UpdateWebView());
			return;
		}
		EpubText.Eval("nextPage()");
	}

	async Task GotoEnd()
	{
		try
		{
			var result = await EpubText.EvaluateJavaScriptAsync("isHorizontallyScrolledToEnd()");
			if (result.Equals("true"))
			{
				isPreviousPage = false;
				return;
			}
			EpubText.Eval("nextPage()");
			await GotoEnd();
		}
		catch (Exception ex)
		{
			logger.Error(ex.Message);
		}
	}

	public async void SwipeGestureRecognizer_Swiped(object? sender, SwipedEventArgs e)
	{
		switch(e.Direction)
		{
			case SwipeDirection.Left:
				await NextPage();
				break;
			case SwipeDirection.Right:
				await PreviousPage();
				break;
			default:
				var viewModel = (BookViewModel)BindingContext;
				viewModel.Press();
				break;
		}
	}

	async Task UpdateWebView()
	{
		Shimmer.IsActive = true;
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		EpubText.Source = new HtmlWebViewSource { Html = html };
		Shimmer.IsActive = false;
		UpdateTheme();
		if(isPreviousPage)
		{
			await GotoEnd();
		}
	}

	void UpdateTheme()
	{
		ArgumentNullException.ThrowIfNull(Application.Current);
		ICollection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries ?? throw new InvalidOperationException();
		var theme = mergedDictionaries.OfType<SyncfusionThemeResourceDictionary>().FirstOrDefault() ?? throw new InvalidOperationException();
		(Color? background, Color? text, Color? navigationColor) = (null, null, null);
		switch (Application.Current?.RequestedTheme)
		{
			case AppTheme.Dark:
				(background, text, navigationColor) = EbookColorScheme.GetColorSchemeColor(EbookColor.Dark);
				theme.VisualTheme = SfVisuals.MaterialLight;
				break;
			case AppTheme.Light:
				(background, text, navigationColor) = EbookColorScheme.GetColorSchemeColor(EbookColor.Default);
				theme.VisualTheme = SfVisuals.MaterialLight;
				break;
		}
		if (background is null || text is null || navigationColor is null)
		{
			return;
		}
		Grid.BackgroundColor = background;
		StackLayout.BackgroundColor = navigationColor;
		PageLabel.BackgroundColor = background;
		PageLabel.TextColor = text;
		Shell.SetBackgroundColor(Application.Current?.Windows[0].Page, navigationColor);
		CurrentPage.BackgroundColor = navigationColor;
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
