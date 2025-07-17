using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Util;

namespace EpubReader.ViewModels;
public partial class FolderDialogePageViewModel : BaseViewModel, IQueryAttributable
{
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	[ObservableProperty]
	public partial string Text { get; set; } = "Please wait...";

	[ObservableProperty]
	public partial List<string> EpubFiles { get; set; } = [];

	[ObservableProperty]
	public partial int Count { get; set; } = 0;

	[ObservableProperty]
	public partial bool ShouldBeVisible { get; set; } = false;

	CancellationTokenSource cancellationTokenSource { get; set; }

	public FolderDialogePageViewModel()
	{
		cancellationTokenSource = new CancellationTokenSource();
		WeakReferenceMessenger.Default.Register<FolderMessage>(this, (r, m) => { Text = $"{m.Value.Title} ({m.Value.Count}/{m.Value.MaxCount})"; });
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			cancellationTokenSource?.Dispose();
			processEpubFiles?.Dispose();
		}
		base.Dispose(disposing);
	}

	[RelayCommand]
	void Cancel()
	{
		cancellationTokenSource.Cancel();
		System.Diagnostics.Debug.WriteLine("Canceling FolderDialogePageViewModel");
		ShouldBeVisible = false;
		cancellationTokenSource = new CancellationTokenSource(); // Reset for future use
	}
	public void OnClose()
	{
		ShouldBeVisible = false;
		System.Diagnostics.Debug.WriteLine("Closing FolderDialogePageViewModel");
		WeakReferenceMessenger.Default.UnregisterAll(this);
	}

	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query is null)
		{
			throw new ArgumentNullException(nameof(query), "Query cannot be null");
		}
		if (query.TryGetValue("Epubfiles", out var EpubObj) && EpubObj is List<string> EpubFilesList)
		{
			EpubFiles = EpubFilesList;
			Count = await processEpubFiles.ProcessEpubFilesAsync(EpubFiles, cancellationTokenSource.Token).ConfigureAwait(false);
			if(Count == EpubFiles.Count || cancellationTokenSource.Token.IsCancellationRequested)
			{
				WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
			}
		}
		else
		{
			throw new InvalidOperationException("Query does not contain a valid 'List<string>' entry");
		}
	}
}
