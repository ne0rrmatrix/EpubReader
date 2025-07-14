using System.Text;
using EpubReader.Models;

namespace EpubReader.Util;

/// <summary>
/// A utility class that provides methods for handling streams related to EPUB books.
/// </summary>
public class StreamExtensions
{
	/// <summary>
	/// Gets the current book associated with the instance.
	/// </summary>
	public Book? Book { get; private set; }

	/// <summary>
	/// Gets the singleton instance of the <see cref="StreamExtensions"/> class.
	/// </summary>
	public static StreamExtensions? Instance { get; private set; }
	public StreamExtensions()
	{
		Instance ??= this;
	}

	/// <summary>
	/// Sets the current book for the instance.
	/// </summary>
	/// <param name="book">The book to be set. Cannot be null.</param>
	public void SetBook(Book book)
	{
		this.Book = book;
	}

	/// <summary>
	/// Retrieves a stream containing the content of the specified URL.
	/// </summary>
	/// <remarks>The method determines the type of the file based on its extension and processes it accordingly. It
	/// returns a memory stream initialized with the file's content. The caller is responsible for disposing of the
	/// stream.</remarks>
	/// <param name="url">The URL of the resource to retrieve. The URL should point to a valid file path.</param>
	/// <returns>A <see cref="Stream"/> containing the content of the file specified by the URL.  If the file is a text file, the
	/// stream contains UTF-8 encoded text.  If the file is a binary file, the stream contains the raw binary data. 
	/// Returns <see cref="Stream.Null"/> if the content cannot be retrieved or the file type is unsupported.</returns>
	public async Task<Stream> GetStream(string url, CancellationToken cancellation = default)
	{
		var filename = System.IO.Path.GetFileName(url);
		var text = Content(filename);
		if (text is not null && IsText(filename))
		{
			UTF8Encoding utfEncoding = new();
			byte[] postData = utfEncoding.GetBytes(
				text);
			MemoryStream postDataStream = new(text.Length);
			await postDataStream.WriteAsync(postData, cancellation);
			postDataStream.Seek(0, SeekOrigin.Begin);
			return postDataStream;
		}
		var bytes = ByteContent(filename);
		if (bytes is not null && IsBinary(filename))
		{
			MemoryStream postDataStream = new(bytes.Length);
			await postDataStream.WriteAsync(bytes, cancellation);
			postDataStream.Seek(0, SeekOrigin.Begin);
			return postDataStream;
		}
		return Stream.Null;
	}

	/// <summary>
	/// Determines the MIME type based on the file extension of the specified file name.
	/// </summary>
	/// <remarks>This method uses a predefined set of common file extensions to determine the MIME type.  If the
	/// file extension is not in the predefined set, the method defaults to "application/octet-stream".</remarks>
	/// <param name="fileName">The name of the file whose MIME type is to be determined. The file name must include an extension.</param>
	/// <returns>A string representing the MIME type corresponding to the file extension.  If the extension is not recognized,
	/// returns "application/octet-stream".</returns>
	public static string GetMimeType(string fileName)
	{
		var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

		return extension switch
		{
			".xhtml" => "application/xhtml+xml",
			".html" or ".htm" => "text/html",
			".css" => "text/css",
			".ico" => "image/x-icon",
			".js" => "application/javascript",
			".json" => "application/json",
			".png" => "image/png",
			".jpg" or ".jpeg" => "image/jpeg",
			".gif" => "image/gif",
			".svg" => "image/svg+xml",
			".pdf" => "application/pdf",
			".txt" => "text/plain",
			".xml" => "application/xml",
			".zip" => "application/zip",
			".rar" => "application/x-rar-compressed",
			".7z" => "application/x-7z-compressed",
			".tar" => "application/x-tar",
			".ttf" => "font/ttf",
			".woff" => "font/woff",
			".woff2" => "font/woff2",
			".eot" => "application/vnd.ms-fontobject",
			".otf" => "font/otf",
			_ => "application/octet-stream"
		};
	}

	/// <summary>
	/// Determines whether the specified file name has an extension that indicates a text file.
	/// </summary>
	/// <remarks>Recognized text file extensions include: .xhtml, .txt, .html, .htm, .css, .js, and .json.</remarks>
	/// <param name="fileName">The name of the file to check, including its extension.</param>
	/// <returns><see langword="true"/> if the file extension is one of the recognized text file types; otherwise, <see
	/// langword="false"/>.</returns>
	public static bool IsText(string fileName)
	{
		var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
		return extension switch
		{
			".xhtml" => true,
			".txt" => true,
			".html" => true,
			".htm" => true,
			".css" => true,
			".js" => true,
			".json" => true,
			_ => false,
		};
	}

	/// <summary>
	/// Determines whether the specified file is a binary file based on its extension.
	/// </summary>
	/// <remarks>This method checks the file extension against a predefined list of common binary file extensions
	/// such as .png, .jpg, .pdf, etc. It returns <see langword="true"/> if the extension matches one of these.</remarks>
	/// <param name="fileName">The name of the file, including its extension, to evaluate.</param>
	/// <returns><see langword="true"/> if the file is considered binary based on its extension; otherwise, <see langword="false"/>.</returns>
	public static bool IsBinary(string fileName)
	{
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		return extension switch
		{
			".png" => true,
			".jpg" => true,
			".jpeg" => true,
			".gif" => true,
			".svg" => true,
			".otf" => true,
			".ttf" => true,
			".woff" => true,
			".woff2" => true,
			".pdf" => true,
			".ico" => true,
			_ => false,
		};
	}

	/// <summary>
	/// Retrieves the HTML content associated with the specified file name.
	/// </summary>
	/// <remarks>The method searches for the file name within the book's chapters, files, and CSS. If a match is
	/// found, it returns the corresponding HTML content. If no match is found or if the instance or book is not
	/// initialized, the method returns <see langword="null"/>.</remarks>
	/// <param name="fileName">The name of the file to search for within the book's chapters, files, and CSS.</param>
	/// <returns>A string containing the HTML content of the file if found; otherwise, <see langword="null"/>.</returns>
	string? Content(string fileName)
	{
		if (Instance is null || Book is null)
		{
			return null;
		}
		fileName = Path.GetFileName(fileName);
		return Book.Chapters.Find(f => f.FileName.Contains(fileName))?.HtmlFile ??
			Book.Files.FirstOrDefault(f => f.FileName.Contains(fileName))?.HTMLContent
			?? Book.Css.ToList().Find(f => f.FileName.Contains(fileName))?.Content
			?? Book.Files.ToList().Find(f => f.FileName.Contains(fileName))?.HTMLContent;
	}

	/// <summary>
	/// Retrieves the byte content of a file specified by its name from the book's images, fonts, or files.
	/// </summary>
	/// <remarks>The method searches for the file in the book's images, fonts, and files collections in that order.
	/// If the <c>Instance</c> or <c>Book</c> is <see langword="null"/>, the method returns <see
	/// langword="null"/>.</remarks>
	/// <param name="fileName">The name of the file to search for within the book's resources. The search is case-sensitive and matches any part
	/// of the file name.</param>
	/// <returns>A byte array containing the content of the file if found; otherwise, <see langword="null"/>.</returns>
	byte[]? ByteContent(string fileName)
	{
		if (Instance is null || Book is null)
		{
			return null;
		}
		fileName = Path.GetFileName(fileName);
		return Book.Images.ToList().Find(f => f.FileName.Contains(fileName))?.Content
			?? Book.Fonts.ToList().Find(f => f.FileName.Contains(fileName))?.Content
		   ?? Book.Files.ToList().Find(f => f.FileName.Contains(fileName))?.Content;
	}
}