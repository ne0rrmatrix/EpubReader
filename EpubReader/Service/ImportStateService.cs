namespace EpubReader.Service;

public sealed partial class ImportStateService : ObservableObject, IImportStateService, IDisposable
{
	readonly IDispatcher dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException();
	CancellationTokenSource cancellationTokenSource = new();
	bool disposedValue;

	[ObservableProperty]
	public partial FolderInfo Current { get; set; } = new();

	[ObservableProperty]
	public partial string Title { get; set; } = "Please wait...";

	[ObservableProperty]
	public partial string CounterText { get; set; } = "0/0";

	[ObservableProperty]
	public partial int Count { get; set; }

	[ObservableProperty]
	public partial int MaxCount { get; set; }

	[ObservableProperty]
	public partial int ProgressPercent { get; set; }

	[ObservableProperty]
	public partial double Progress { get; set; }

	[ObservableProperty]
	public partial bool IsRunning { get; set; }

	public CancellationToken Token => cancellationTokenSource.Token;

	public void Begin(int maxCount = 0)
	{
		if (cancellationTokenSource.IsCancellationRequested)
		{
			cancellationTokenSource.Dispose();
			cancellationTokenSource = new CancellationTokenSource();
		}

		if (!dispatcher.IsDispatchRequired)
		{
			IsRunning = true;
			ApplyProgress(string.Empty, 0, maxCount);
			return;
		}

		dispatcher.Dispatch(() =>
		{
			IsRunning = true;
			ApplyProgress(string.Empty, 0, maxCount);
		});
	}

	public void ReportProgress(string title, int count, int maxCount)
	{
		if (!dispatcher.IsDispatchRequired)
		{
			ApplyProgress(title, count, maxCount);
			return;
		}

		dispatcher.Dispatch(() => ApplyProgress(title, count, maxCount));
	}

	void ApplyProgress(string title, int count, int maxCount)
	{
		Current = new FolderInfo
		{
			Title = title,
			Count = count,
			MaxCount = maxCount,
		};
		Title = string.IsNullOrWhiteSpace(title) ? "Please wait..." : title;
		Count = count;
		MaxCount = maxCount;
		CounterText = $"{count}/{maxCount}";
		UpdateProgress();
	}

	public void Cancel()
	{
		if (!cancellationTokenSource.IsCancellationRequested)
		{
			cancellationTokenSource.Cancel();
		}
	}

	public void Complete()
	{
		if (!dispatcher.IsDispatchRequired)
		{
			IsRunning = false;
			return;
		}

		dispatcher.Dispatch(() => IsRunning = false);
	}

	void Dispose(bool disposing)
	{
		if (disposedValue)
		{
			return;
		}

		if (disposing)
		{
			cancellationTokenSource.Dispose();
		}

		disposedValue = true;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	partial void OnCountChanged(int value)
	{
		CounterText = $"{value}/{MaxCount}";
		UpdateProgress();
	}

	partial void OnMaxCountChanged(int value)
	{
		CounterText = $"{Count}/{value}";
		UpdateProgress();
	}

	void UpdateProgress()
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

}