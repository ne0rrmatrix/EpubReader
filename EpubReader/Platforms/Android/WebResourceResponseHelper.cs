using Android.OS;
using Android.Webkit;

namespace EpubReader.Platforms.Android;
public static class WebResourceResponseHelper
{
	/// <summary>
	/// Creates a WebResourceResponse from an HTML stream.
	/// </summary>
	/// <param name="stream">The HTML Stream</param>
	/// <param name="mimeType">The MIME type (defaults to "text/html")</param>
	/// <param name="statusCode">HTTP status code (defaults to 200)</param>
	/// <param name="reasonPhrase">HTTP reason phrase (defaults to "OK")</param>
	/// <param name="additionalHeaders">Additional HTTP headers to include</param>
	/// <returns>A WebResourceResponse containing the HTML content</returns>
	public static WebResourceResponse CreateFromHtmlString(
		Stream stream,
		string mimeType,
		int statusCode = 200,
		string reasonPhrase = "OK",
		Dictionary<string, string> additionalHeaders = null!)
	{
		var reader = new StreamReader(stream, true);
		var memoryStream = new MemoryStream();
		reader.BaseStream.CopyTo(memoryStream);
		var contentBytes = memoryStream.ToArray();

		// Create a memory stream from the byte array
		MemoryStream contentStream = new(contentBytes);

		// Create headers dictionary if not provided
		Dictionary<string, string> headers = additionalHeaders ?? [];

		// Add Content-Length header if not already present
		if (!headers.ContainsKey("Content-Length"))
		{
			headers.Add("Content-Length", contentBytes.Length.ToString());
		}

		// For API level 21+ (Lollipop and above), we can set status code and headers
		if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
		{
			return new WebResourceResponse(mimeType, reader.CurrentEncoding.BodyName.ToUpper(), statusCode, reasonPhrase, headers, contentStream);
		}
		else
		{
			// For older API levels, we can only set MIME type, encoding, and data
			return new WebResourceResponse(mimeType, reader.CurrentEncoding.BodyName.ToUpper(), contentStream);
		}
	}
}

