namespace EpubReader.Service;

public class ReaderSettingsStateService(IDb db) : IReaderSettingsStateService
{
	readonly IDb db = db;

	public event EventHandler<ReaderSettingsChangedEventArgs>? SettingsChanged;

	public Settings? CurrentSettings { get; private set; }

	public async Task<Settings> GetCurrentAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		CurrentSettings = await db.GetSettings(CancellationToken.None) ?? new Settings();
		return CurrentSettings;
	}

	public async Task SaveAsync(Settings settings, SettingsChangeKind changeKind, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(settings);
		await db.SaveSettings(settings, CancellationToken.None);
		CurrentSettings = settings;
		SettingsChanged?.Invoke(this, new ReaderSettingsChangedEventArgs(changeKind));
	}

	public async Task<Settings> ResetAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		await db.RemoveAllSettings(CancellationToken.None);
		var settings = new Settings();
		await db.SaveSettings(settings, CancellationToken.None);
		CurrentSettings = settings;
		SettingsChanged?.Invoke(this, new ReaderSettingsChangedEventArgs(SettingsChangeKind.Reset));
		return settings;
	}
}
