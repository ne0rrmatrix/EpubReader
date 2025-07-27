using System.Globalization;
using EpubReader.Models;
using Plugin.Maui.Audio;

namespace EpubReader.Service;
public class AudioPlayer
{
	IDispatcher Dispatcher { get; }
	List<AudioCue> cues = [];
	IDispatcherTimer? timer;
	AsyncAudioPlayer? asyncAudioPlayer;
	WebView? webView;
	Book? book;
	string currentItemId = string.Empty;
	readonly IAudioManager audioManager = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IAudioManager>()
		?? throw new InvalidOperationException("AudioManager is not available in the current context.");

	public AudioPlayer(IDispatcher dispatcher)
	{
		Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		System.Diagnostics.Debug.WriteLine("audioPlayer initialized.");
	}

	public async Task PlayAudio(Book book, WebView webview)
	{
		System.Diagnostics.Debug.WriteLine("PlayAudio called.");
		webView = webview;
		this.book = book ?? throw new ArgumentNullException(nameof(book));
		var audioData = book.Chapters[book.CurrentChapter].AudioCues.Find(cue => cue.Text == book.Chapters[book.CurrentChapter].FileName)?.AudioData 
			?? throw new InvalidOperationException("Audio data is not available for the current chapter.");
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

	public void StopAudio()
	{
		asyncAudioPlayer?.Stop();
		ArgumentNullException.ThrowIfNull(webView);
		ClearTimer();
		Dispatcher.Dispatch(async () 
			=> { await webView.EvaluateJavaScriptAsync($"removeHighlight();");});
	}

	void UpdatePlaybackHighlight()
	{
		ArgumentNullException.ThrowIfNull(webView);
		ArgumentNullException.ThrowIfNull(book);
		ArgumentNullException.ThrowIfNull(asyncAudioPlayer);
		var cue = cues.Find(cue => 
			TimeSpan.TryParse(cue.ClipBegin, new CultureInfo("en-US"), out var begin) &&
			TimeSpan.TryParse(cue.ClipEnd, new CultureInfo("en-US"), out var end) &&
			asyncAudioPlayer.CurrentPosition >= begin.TotalSeconds && 
			asyncAudioPlayer.CurrentPosition <= end.TotalSeconds);
		if(cue is not null && !cue.SpandId.Equals(currentItemId))
		{
			currentItemId = cue.SpandId;
			RunCode(cue);
			return;
		}
	}
	void RunCode(AudioCue cue)
	{
		ArgumentNullException.ThrowIfNull(webView);
		ArgumentNullException.ThrowIfNull(book);
		string result = string.Empty;
		Dispatcher.Dispatch(async () => result = await webView.EvaluateJavaScriptAsync($"highlightSpan('{cue.SpandId}');"));
		
		if (result.Contains("true", StringComparison.CurrentCultureIgnoreCase))
		{
			return; // Exit to prevent further processing of this cue
		}

		if (result.Contains("false", StringComparison.CurrentCultureIgnoreCase))
		{
			System.Diagnostics.Debug.WriteLine($"HighlightSpan returned false for {cue.SpandId}, indicating no highlight was applied.");
		}
	}

	void OnTimerTick(object? sender, EventArgs e)
	{
		UpdatePlaybackHighlight();
	}

	void InitializeTimer()
	{
		if (timer is not null)
		{
			return;
		}

		timer = Dispatcher.CreateTimer();
		timer.Interval = TimeSpan.FromMilliseconds(200);
		timer.Tick += OnTimerTick;
		timer.Start();
	}

	void ClearTimer()
	{
		if (timer is null)
		{
			return;
		}

		timer.Tick -= OnTimerTick;
		timer.Stop();
		timer = null;
	}

}