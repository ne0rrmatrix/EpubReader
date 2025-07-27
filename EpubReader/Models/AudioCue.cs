namespace EpubReader.Models;

/// <summary>
/// Represents an audio cue with associated metadata, including identifiers and timing information.
/// </summary>
public class AudioCue
{
	/// <summary>
	/// Gets or sets the unique identifier for the entity.
	/// </summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the text content.
	/// </summary>
	public string Text { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the starting point of the clip in a media timeline.
	/// </summary>
	public string ClipBegin { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the ending point of the clip in a media timeline.
	/// </summary>
	public string ClipEnd { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the unique identifier for the span.
	/// </summary>
	public string SpandId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the audio data as a byte array.
	/// </summary>
	public byte[] AudioData { get; set; } = [];

	/// <summary>
	/// Gets or sets the name of the file.
	/// </summary>
	public string FileName { get; set; } = string.Empty;
}
