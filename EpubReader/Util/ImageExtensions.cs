namespace EpubReader.Util;

/// <summary>
/// A utility class that provides methods for detecting image file types based on their content.
/// </summary>
public static class ImageExtensions
{
	static readonly List<string> jpgSignature = ["FF", "D8"];
	static readonly List<string> bmpSignature = ["42", "4D"];
	static readonly List<string> gifSignature = ["47", "49", "46"];
	static readonly List<string> pngSignature = ["89", "50", "4E", "47", "0D", "0A", "1A", "0A"];

	const string jpgFirstByte = "FF";
	const string bmpFirstByte = "42";
	const string gifFirstByte = "47";
	const string pngFirstByte = "89";

	/// <summary>
	/// Detects the file extension based on file content
	/// </summary>
	/// <param name="file">Path to the file</param>
	/// <returns>File extension (jpg, bmp, gif, png) or empty string if not detected</returns>
	public static string GetFileExtension(this string file)
	{
		if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
		{
			return string.Empty;
		}

		using var stream = File.OpenRead(file);
		return stream.GetFileExtension();
	}

	/// <summary>
	/// Detects the file extension based on stream content
	/// </summary>
	/// <param name="stream">Stream to analyze</param>
	/// <returns>File extension (jpg, bmp, gif, png) or empty string if not detected</returns>
	public static string GetFileExtension(this Stream stream)
	{
		stream.Seek(0, SeekOrigin.Begin);
		string firstByte = stream.ReadByte().ToString("X2");

		return firstByte switch
		{
			jpgFirstByte when stream.MatchesSignature(jpgSignature) => "jpg",
			bmpFirstByte when stream.MatchesSignature(bmpSignature) => "bmp",
			gifFirstByte when stream.MatchesSignature(gifSignature) => "gif",
			pngFirstByte when stream.MatchesSignature(pngSignature) => "png",
			_ => string.Empty
		};
	}

	static bool MatchesSignature(this Stream stream, List<string> signature)
	{
		stream.Seek(0, SeekOrigin.Begin);
		foreach (string expectedByte in signature)
		{
			string actualByte = stream.ReadByte().ToString("X2");
			if (string.Compare(actualByte, expectedByte, StringComparison.OrdinalIgnoreCase) != 0)
			{
				return false;
			}
		}
		return true;
	}
}
