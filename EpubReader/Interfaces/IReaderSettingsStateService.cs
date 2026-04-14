namespace EpubReader.Interfaces;

public interface IReaderSettingsStateService
{
	event EventHandler<ReaderSettingsChangedEventArgs>? SettingsChanged;

	Settings? CurrentSettings { get; }

	Task<Settings> GetCurrentAsync(CancellationToken token = default);
	Task SaveAsync(Settings settings, SettingsChangeKind changeKind, CancellationToken token = default);
	Task<Settings> ResetAsync(CancellationToken token = default);
}