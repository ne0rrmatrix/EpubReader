using System.Globalization;
using EpubReader.Models;
using MetroLog;
using Plugin.Maui.Audio;

namespace EpubReader.Service;
public partial class AudioPlayer
{
	readonly ILogger logger = LoggerFactory.GetLogger(nameof(AudioPlayer));
	readonly IDispatcher dispatcher = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDispatcher>()
		?? throw new InvalidOperationException("Dispatcher is not available in the current context.");
	List<AudioCue> cues = [];
	IDispatcherTimer? timer;
	AsyncAudioPlayer? asyncAudioPlayer;
	public AsyncAudioPlayer? AsyncAudioPlayer => asyncAudioPlayer;
	WebView? webView;
	Book? book;
	string currentItemId = string.Empty;
	string seekToCueId = string.Empty;
	readonly IAudioManager audioManager = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IAudioManager>()
		?? throw new InvalidOperationException("AudioManager is not available in the current context.");

	public AudioPlayer()
	{
		logger.Info("audioPlayer initialized.");
	}

	/// <summary>
	/// Seeks the audio player to the position specified by the cue identifier.
	/// </summary>
	/// <remarks>If the specified <paramref name="cueId"/> does not correspond to any existing cue, the method logs
	/// a warning and returns without seeking. If the cue's start time is invalid, a warning is logged and no seeking
	/// occurs.</remarks>
	/// <param name="cueId">The identifier of the cue to seek to. This value is case-insensitive.</param>
	public void SeekTo(string? cueId)
	{
		if (string.IsNullOrEmpty(cueId))
		{
			logger.Warn("SeekTo called with null or empty cueId.");
			return;
		}
		if(!string.IsNullOrEmpty(seekToCueId))
		{
			seekToCueId = string.Empty;
		}
		logger.Info($"SeekTo called with cueId: {cueId}");
		var cue = cues.Find(c => c.SpandId.Equals(cueId, StringComparison.OrdinalIgnoreCase));
		if (cue is null)
		{
			logger.Warn($"No cue found with ID: {cueId}");
			return;
		}
		if (TimeSpan.TryParse(cue.ClipBegin, new CultureInfo("en-US"), out var begin))
		{
			var position = begin.TotalSeconds;
			logger.Info($"Seeking audio player to position: {position}");
			
			asyncAudioPlayer?.Seek(position);
		}
		else
		{
			logger.Warn($"Invalid cue times for cue ID: {cueId}");
		}
	}

	/// <summary>
	/// Plays the audio for the current chapter of the specified book using the provided web view.
	/// </summary>
	/// <remarks>If audio is already playing, the method logs a message and returns without taking further action.
	/// The method initializes the audio player and starts playback for the current chapter's audio cues. It also interacts
	/// with the web view to manage UI elements related to audio playback.</remarks>
	/// <param name="book">The book containing the audio data to be played. Cannot be <see langword="null"/>.</param>
	/// <param name="webview">The web view used to interact with the UI during audio playback.</param>
	/// <returns></returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="book"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">Thrown if the audio cues are not available for the current chapter or if the audio player could not be created.</exception>
	public async Task PlayAudio(Book book, WebView webview, string? cueId)
	{

		if(asyncAudioPlayer is not null && asyncAudioPlayer.CurrentPosition > 0)
		{
			SeekTo(cueId);
			logger.Info("Audio is already playing, ignoring function call for it again.");
			return;
		}
		seekToCueId = cueId ?? string.Empty;
		logger.Info("PlayAudio called.");
		webView = webview;
		this.book = book ?? throw new ArgumentNullException(nameof(book));
		var audioData = book.Chapters[book.CurrentChapter].AudioCues.Find(cue => cue.Text == book.Chapters[book.CurrentChapter].FileName)?.AudioData
			?? [];
		if (audioData.Length == 0)
		{
			logger.Info($"End of book reached. Stopping audio and exiting audio player.");
			dispatcher.Dispatch(async () => await webview.EvaluateJavaScriptAsync("removeHighlight();"));
			StopAudio();
			return;
		}
		cues = book.Chapters[book.CurrentChapter].AudioCues 
		?? throw new InvalidOperationException("AudioCues are not available for the current chapter.");
		
		var memoryStream = new MemoryStream();
		await memoryStream.WriteAsync(audioData).ConfigureAwait(false);
		memoryStream.Seek(0, SeekOrigin.Begin);
		
		asyncAudioPlayer = audioManager.CreateAsyncPlayer(memoryStream)
			?? throw new InvalidOperationException($"Audio player could not be created for chapter '{book.Chapters[book.CurrentChapter].Title}'.");
		asyncAudioPlayer.Loop = false;
		await webview.EvaluateJavaScriptAsync("setAutoPageFlip('true');");
		InitializeTimer();

		await asyncAudioPlayer.PlayAsync(CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Stops the audio playback and clears any associated timers.
	/// </summary>
	/// <remarks>This method stops the audio player if it is currently playing and ensures that any visual
	/// highlights in the web view are removed. It also clears any active timers related to the audio playback.</remarks>
	public void StopAudio()
	{
		logger.Info("StopAudio called.");
		asyncAudioPlayer?.Stop();
		ArgumentNullException.ThrowIfNull(webView);
		ClearTimer();
		
		dispatcher.Dispatch(async () 
			=> { await webView.EvaluateJavaScriptAsync($"removeHighlight();");});
	}

	async Task UpdatePlaybackHighlight()
	{
		ArgumentNullException.ThrowIfNull(webView);
		ArgumentNullException.ThrowIfNull(book);
		ArgumentNullException.ThrowIfNull(asyncAudioPlayer);
		if(!string.IsNullOrEmpty(seekToCueId))
		{
			logger.Info($"Seeking to cue with ID: {seekToCueId}");
			SeekTo(seekToCueId);
			seekToCueId = string.Empty;
			return;
		}
		var cue = cues.Find(cue => 
			TimeSpan.TryParse(cue.ClipBegin, new CultureInfo("en-US"), out var begin) &&
			TimeSpan.TryParse(cue.ClipEnd, new CultureInfo("en-US"), out var end) &&
			asyncAudioPlayer.CurrentPosition >= begin.TotalSeconds && 
			asyncAudioPlayer.CurrentPosition <= end.TotalSeconds);
		ArgumentNullException.ThrowIfNull(asyncAudioPlayer);

		if (cue is not null && !cue.SpandId.Equals(currentItemId))
		{
			currentItemId = cue.SpandId;
			await RunCode(cue);
			return;
		}
		
		if (asyncAudioPlayer.CurrentPosition.Equals(0.00))
		{
			logger.Info("Last cue reached, stopping audio.");
			StopAudio();
			await webView.EvaluateJavaScriptAsync("nextChapter()");
			return;
		}
	}
	async Task RunCode(AudioCue cue)
	{
		ArgumentNullException.ThrowIfNull(webView);
		ArgumentNullException.ThrowIfNull(book);
		var result = string.Empty;
		result = await webView.EvaluateJavaScriptAsync($"isSpanOnNextPage('{cue.SpandId}');");
		
		if (result.Equals("true", StringComparison.OrdinalIgnoreCase))
		{
			dispatcher.Dispatch(async () =>
			{
				await webView.EvaluateJavaScriptAsync($"handleNext();");
				await webView.EvaluateJavaScriptAsync($"highlightSpan('{cue.SpandId}')");
				await webView.EvaluateJavaScriptAsync($"updateVisibleSpanElements();");
			});
			return;
		}
		
		dispatcher.Dispatch(async () => await webView.EvaluateJavaScriptAsync($"highlightSpan('{cue.SpandId}');"));
		dispatcher.Dispatch(async () => await webView.EvaluateJavaScriptAsync($"updateVisibleSpanElements();"));
	}


	async void OnTimerTick(object? sender, EventArgs e)
	{
		await UpdatePlaybackHighlight().ConfigureAwait(false);
	}

	void InitializeTimer()
	{
		if (timer is not null)
		{
			logger.Info("Timer already initialized, skipping initialization.");
			return;
		}
		logger.Info("Initializing timer for audio playback updates.");
		timer = dispatcher.CreateTimer();
		timer.Interval = TimeSpan.FromMilliseconds(200);
		timer.Tick += OnTimerTick;
		timer.Start();
	}

	void ClearTimer()
	{
		if (timer is null)
		{
			logger.Info("Timer is already null, skipping clear operation.");
			return;
		}
		logger.Info("Clearing timer for audio playback updates.");
		timer.Tick -= OnTimerTick;
		timer.Stop();
		timer = null;
	}
}