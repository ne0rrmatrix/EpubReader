using Plugin.Maui.Audio;

namespace EpubReader.Service;

/// <summary>
/// Encapsulates audio playback session management so callers don't need
/// to manage player streams and events directly.
/// </summary>
public partial class AudioPlaybackService(IAudioManager audioManager) : IDisposable
{
	bool disposed = false;
	readonly IAudioManager audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
	IAudioPlayer? player;
	Stream? stream;

	public bool HasSession => player is not null;
	public string? CurrentResourceId { get; private set; }
	public bool IsPlaying => player?.IsPlaying ?? false;
	public double CurrentPosition => player?.CurrentPosition ?? double.NaN;

	public event EventHandler? PlaybackEnded;

	public Task<bool> OpenResourceAsync(string resourceId, byte[] content)
	{
		DisposeSession();

		stream = new MemoryStream(content, writable: false);
		player = audioManager.CreatePlayer(stream);
		if (player is null)
		{
			DisposeStream();
			CurrentResourceId = null;
			return Task.FromResult(false);
		}

		player.PlaybackEnded += OnPlayerEnded;
		CurrentResourceId = resourceId;
		return Task.FromResult(true);
	}

	void OnPlayerEnded(object? s, EventArgs e)
	{
		PlaybackEnded?.Invoke(this, EventArgs.Empty);
	}

	public void Seek(double seconds)
	{
		player?.Seek(seconds);
	}

	public void Play()
	{
		if (player is not null && !player.IsPlaying)
		{
			player.Play();
		}
	}

	public void Pause()
	{
		player?.Pause();
	}

	public void Stop()
	{
		if (player is not null)
		{
			player.PlaybackEnded -= OnPlayerEnded;
			player.Stop();
		}
	}

	void DisposeStream()
	{
		if (stream is not null)
		{
			try
			{
				stream.Dispose();
			}
			catch (Exception)
			{
				// Ignored: disposal errors are non-fatal for session cleanup
			}
			stream = null;
		}
	}

	void DisposeSession()
	{
		if (player is not null)
		{
			try
			{
				player.PlaybackEnded -= OnPlayerEnded;
				player.Dispose();
			}
			catch (Exception)
			{
				// Ignored: best-effort disposal
			}
			player = null;
		}

		DisposeStream();
		CurrentResourceId = null;
	}
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposed)
		{
			return;
		}
		if (disposing)
		{
			DisposeSession();
		}
		disposed = true;
	}
}