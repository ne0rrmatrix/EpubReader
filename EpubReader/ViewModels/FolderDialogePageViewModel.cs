using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Util;

namespace EpubReader.ViewModels;
public partial class FolderDialogPageViewModel : BaseViewModel, IQueryAttributable
{
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	[ObservableProperty]
	public partial string Text { get; set; } = "Please wait...";

	/// <summary>
	/// Gets or sets the list of EPUB file paths.
	/// </summary>
	[ObservableProperty]
	public partial List<string> EpubFiles { get; set; } = [];

	/// <summary>
	/// Gets or sets the current count value.
	/// </summary>
	[ObservableProperty]
	public partial int Count { get; set; } = 0;

	/// <summary>
	/// Gets or sets a value indicating whether the element should be visible.
	/// </summary>
	[ObservableProperty]
	public partial bool ShouldBeVisible { get; set; } = false;

	CancellationTokenSource cancellationTokenSource { get; set; } = new CancellationTokenSource();

	public FolderDialogPageViewModel()
	{
		WeakReferenceMessenger.Default.Register<FolderMessage>(this, (r, m) => { Text = $"{m.Value.Title} ({m.Value.Count}/{m.Value.MaxCount})"; });
	}

	/// <summary>
	/// Releases the unmanaged resources used by the class and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only
	/// unmanaged resources.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			cancellationTokenSource?.Dispose();
			processEpubFiles?.Dispose();
		}
		base.Dispose(disposing);
	}

	/// <summary>
	/// Cancels the ongoing operation and updates the visibility state.
	/// </summary>
	/// <remarks>This method triggers the cancellation of the current operation by signaling the associated <see
	/// cref="CancellationTokenSource"/>. After cancellation, it sets the visibility state to indicate that the operation
	/// is no longer active.</remarks>
	[RelayCommand]
	void Cancel()
	{
		cancellationTokenSource.Cancel();
		ShouldBeVisible = false;
	}

	/// <summary>
	/// Handles the closing event by updating visibility and unregistering from message notifications.
	/// </summary>
	/// <remarks>This method sets the visibility flag to false and unregisters the current instance from all
	/// messages using the default messenger. It should be called when the object is being closed or disposed to ensure
	/// proper cleanup.</remarks>
	public void OnClose()
	{
		ShouldBeVisible = false;
		WeakReferenceMessenger.Default.UnregisterAll(this);
	}

	/// <summary>
	/// Applies query attributes to process a list of EPUB files asynchronously.
	/// </summary>
	/// <remarks>This method processes the EPUB files specified in the query and updates the count of processed
	/// files. If the processing completes successfully or is canceled, a <see cref="SettingsMessage"/> is sent via the
	/// <see cref="WeakReferenceMessenger"/>.</remarks>
	/// <param name="query">A dictionary containing query parameters. Must include a key "Epubfiles" with a value of type <see
	/// cref="List{String}"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="query"/> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown if the query does not contain a valid 'List&lt;string&gt;' entry under the key "Epubfiles".</exception>
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
