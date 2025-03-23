#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
#endif

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Message;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.ViewModels;
using MetroLog;
using Microsoft.Maui.Storage;

namespace EpubReader.Views;

public partial class BookPage : ContentPage, IDisposable
{
#if ANDROID
	readonly CommunityToolkit.Maui.Behaviors.TouchBehavior touchbehavior = new();
#endif
	readonly IDb db;
	Book book = new();
	Settings settings = new();
	bool disposedValue;

	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		EpubText.SetInvokeJavaScriptTarget(this);
		this.db = db;
		
#if ANDROID
		//EpubText.Behaviors.Add(touchbehavior);
#endif
	}

	async void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book;
		settings = await db.GetSettings(CancellationToken.None);
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
		await EpubText.EvaluateJavaScriptAsync($"setIframeSource(\"{file}\")");
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		Shimmer.IsActive = false;
	}

	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None);
		
		List<string> background = GetProperty(settings.SetBackgroundColor);
		List<string> text = GetProperty(settings.SetTextColor);
		if(background.Count > 1)
		{
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('{background[0]}', '{background[1]}')");
			await EpubText.EvaluateJavaScriptAsync($"setBackgroundColor('{settings.BackgroundColor}')");
		}
		if (text.Count > 1)
		{
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('{text[0]}', '{text[1]}')");
		}
		await EpubText.EvaluateJavaScriptAsync("setReadiumProperty('--USER__advancedSettings', 'readium-advanced-on')");
		await EpubText.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')");
		await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontFamily', '{settings.FontFamily}')");
		await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{settings.FontSize*10}%')");
	}

	static List<string> GetProperty(string key)
	{
		var temp = key.Split(":");
		if (temp.Length > 1)
		{
			return [temp[0], temp[1]];
		}
		return [];
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
					PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
					var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
					await EpubText.EvaluateJavaScriptAsync($"setIframeSource(\"{file}\")");
					await db.SaveBookData(book, CancellationToken.None);
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

	async void PreviousPage(object sender, EventArgs e)
	{
		if (book.CurrentChapter > 0)
		{
			book.CurrentChapter--;
			await db.SaveBookData(book, CancellationToken.None);
			var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
			await EpubText.EvaluateJavaScriptAsync($"setIframeSource(\"{file}\")");
			await EpubText.EvaluateJavaScriptAsync("scrollToHorizontalEnd()");
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		}
	}

	async void NextPage(object sender, EventArgs e)
	{
		if (book.CurrentChapter < book.Chapters.Count - 1)
		{
			book.CurrentChapter++;
			await db.SaveBookData(book, CancellationToken.None);
			var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
			await EpubText.EvaluateJavaScriptAsync($"setIframeSource(\"{file}\")");
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		}
	}

	public void SwipeGestureRecognizer_Swiped(object? sender, SwipedEventArgs e)
	{
		if(sender is null)
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
	public void DoSyncWork()
	{
		Debug.WriteLine("DoSyncWork");
		NextPage(this, new EventArgs());
	}
	public void DoSomeWork1()
	{
		Debug.WriteLine("DoSomeWork1");
		PreviousPage(this, new EventArgs());
	}
}
