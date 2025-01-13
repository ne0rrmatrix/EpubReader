using CommunityToolkit.Mvvm.ComponentModel;
using EpubReader.Interfaces;
using EpubReader.Models;
using MetroLog;

namespace EpubReader.ViewModels;

public partial class BaseViewModel : ObservableObject, IDisposable
{
	readonly Task? openSettings;
	readonly CancellationTokenSource? cts;
	public readonly IDispatcher Dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException();
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BaseViewModel));
	public IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	Settings settings = new();
	bool disposedValue;

	Book book = new();
	public Book Book
	{
		get => book;
		set
		{
			SetProperty(ref book, value);
			//IsNavMenuVisible = false;
		}
	}
	public Settings Settings
	{
		get => settings;
		set => SetProperty(ref settings, value);
	}

	public BaseViewModel()
	{
		cts = new CancellationTokenSource();
		openSettings = GetSettings(cts.Token);
		if(openSettings.IsFaulted)
		{
			logger.Error($"Failed to get settings: {openSettings.Exception}");
		}
	}

	async Task GetSettings(CancellationToken cancellationToken = default)
	{
		Settings = await db.GetSettings(cancellationToken).ConfigureAwait(true);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				openSettings?.Dispose();
				cts?.Dispose();
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
