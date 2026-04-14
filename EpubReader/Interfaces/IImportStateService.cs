using System.ComponentModel;

namespace EpubReader.Interfaces;

public interface IImportStateService : INotifyPropertyChanged
{
	FolderInfo Current { get; }
	string Title { get; }
	string CounterText { get; }
	int Count { get; }
	int MaxCount { get; }
	int ProgressPercent { get; }
	double Progress { get; }
	bool IsRunning { get; }
	CancellationToken Token { get; }

	void Begin(int maxCount = 0);
	void ReportProgress(string title, int count, int maxCount);
	void Cancel();
	void Complete();
}