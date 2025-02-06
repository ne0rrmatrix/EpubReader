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
using MetroLog;
using Syncfusion.Maui.Toolkit.Themes;

namespace EpubReader.Views;

public partial class BookPage : ContentPage
{
	readonly IDb db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
	Book book = new();
	Settings settings = new();
	public BookPage(BookViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
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

		Dispatcher.Dispatch(() =>
		{
			Shimmer.IsActive = true;
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		});

		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);

		Dispatcher.Dispatch(() =>
		{
			EpubText.Source = new HtmlWebViewSource { Html = html };
			Shimmer.IsActive = false;
			UpdateTheme();
		});
	}


	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(false);
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		Dispatcher.Dispatch(() =>
		{
			EpubText.Source = new HtmlWebViewSource { Html = html };
			UpdateTheme();
		});
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
				Dispatcher.Dispatch(() =>
				{
					EpubText.Source = new HtmlWebViewSource { Html = html };
					PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
				});
			})
		};
		Shell.Current.ToolbarItems.Add(toolbarItem);
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		EpubText.Navigating -= EpubText_Navigating;
		EpubText.Navigated -= OnEpubText_Navigated;
		ArgumentNullException.ThrowIfNull(Application.Current);
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
		Dispatcher.Dispatch(() => Shimmer.IsActive = true);
		book.CurrentChapter--;
		await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);

		Dispatcher.Dispatch(() =>
		{
			EpubText.Source = new HtmlWebViewSource { Html = html };
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
			Shimmer.IsActive = false;
		});
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
		Dispatcher.Dispatch(() => Shimmer.IsActive = true);
		book.CurrentChapter++;
		await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		Dispatcher.Dispatch(() =>
		{
			EpubText.Source = new HtmlWebViewSource { Html = html };
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
			Shimmer.IsActive = false;
		});
	}

	public async void SwipeGestureRecognizer_Swiped(object sender, SwipedEventArgs e)
	{
		if (e.Direction == SwipeDirection.Left)
		{
			await NextPage();
		}
		else if (e.Direction == SwipeDirection.Right)
		{
			await PreviousPage();
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
}
