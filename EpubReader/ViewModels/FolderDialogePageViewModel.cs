namespace EpubReader.ViewModels;

public partial class FolderDialogPageViewModel : BaseViewModel
{
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	[ObservableProperty]
	public partial string Text { get; set; } = "Please wait...";

	[ObservableProperty]
	public partial string CounterText { get; set; } = "0/0";
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
	/// Maximum count of items to be processed.
	/// </summary>
	[ObservableProperty]
	public partial int MaxCount { get; set; } = 0;

	/// <summary>
	/// Progress as integer percent (0..100).
	/// </summary>
	[ObservableProperty]
	public partial int ProgressPercent { get; set; } = 0;

	/// <summary>
	/// Progress as double (0..1) for binding to ProgressBar.Progress.
	/// </summary>
	[ObservableProperty]
	public partial double Progress { get; set; } = 0.0;

	public FolderDialogPageViewModel()
	{
		WeakReferenceMessenger.Default.Register<FolderMessage>(this, (r, m) => {
			var info = m.Value;
			Text = $"{info.Title}";
			CounterText = $"{info.Count}/{info.MaxCount}";
			Count = info.Count;
			MaxCount = info.MaxCount;
		});
	}

	partial void OnCountChanged(int value)
	{
		UpdateProgress();
	}

	partial void OnMaxCountChanged(int value)
	{
		UpdateProgress();
	}

	void UpdateProgress()
	{
		try
		{
			if (MaxCount <= 0)
			{
				ProgressPercent = 0;
				Progress = 0.0;
				return;
			}
			var percent = Math.Min(100, (int)Math.Floor((double)Count * 100.0 / MaxCount));
			ProgressPercent = percent;
			Progress = Math.Max(0.0, Math.Min(1.0, percent / 100.0));
		}
		catch
		{
			ProgressPercent = 0;
			Progress = 0.0;
		}
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
		WeakReferenceMessenger.Default.Send(new CalibreMessage(true));
	}

	/// <summary>
	/// Handles the closing event by updating visibility and unregistering from message notifications.
	/// </summary>
	/// <remarks>This method sets the visibility flag to false and unregisters the current instance from all
	/// messages using the default messenger. It should be called when the object is being closed or disposed to ensure
	/// proper cleanup.</remarks>
	public void OnClose()
	{
		WeakReferenceMessenger.Default.UnregisterAll(this);
	}
}