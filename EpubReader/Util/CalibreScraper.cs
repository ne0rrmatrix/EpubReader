using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using EpubReader.Models;
using HtmlAgilityPack;
using MetroLog;

namespace EpubReader.Util;
public partial class CalibreScraper(int delayBetweenRequestsMs = 100) : IDisposable
{
	readonly HttpClient httpClient = new();
	readonly int delayBetweenRequests = delayBetweenRequestsMs;
	bool disposed;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(CalibreScraper));

	/// <summary>
	/// Fetches the HTML content from a given URL.
	/// </summary>
	/// <param name="url">The URL to fetch HTML from.</param>
	/// <returns>The HTML content as a string, or null if an error occurs.</returns>
	async Task<string?> GetHtmlContentAsync(string url)
	{
		try
		{
			// Send a GET request to the specified URL
			HttpResponseMessage response = await httpClient.GetAsync(url);

			// Ensure the request was successful (status code 200-299)
			response.EnsureSuccessStatusCode();

			// Read the response content as a string
			string htmlContent = await response.Content.ReadAsStringAsync();
			return htmlContent;
		}
		catch (HttpRequestException httpEx)
		{
			logger.Warn($"HTTP Request Error: {httpEx.Message}");
			// Handle specific HTTP errors (e.g., 404 Not Found, 500 Internal Server Error)
			return null;
		}
		catch (Exception ex)
		{
			logger.Warn($"An unexpected error occurred while fetching HTML: {ex.Message}");
			return null;
		}
	}
	/// <summary>
	/// Parses the HTML content to extract the total number of books.
	/// </summary>
	/// <param name="htmlContent">The HTML content of the Calibre server page.</param>
	/// <returns>The total number of books as an integer, or -1 if not found or an error occurs.</returns>
	static int ParseNumberOfBooksFromHtml(string htmlContent)
	{
		if (string.IsNullOrWhiteSpace(htmlContent))
		{
			// No Console.WriteLine here if you strictly want only int return for public method.
			// But for debugging, it's useful.
			return -1;
		}

		var htmlDoc = new HtmlDocument();
		htmlDoc.LoadHtml(htmlContent);

		var navigationSpan = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='navigation']/span[contains(text(), 'Books 1 to')]");

		if (navigationSpan is not null)
		{
			string spanText = navigationSpan.InnerText;
			Match match = MatchNumber().Match(spanText);

			if (match.Success && int.TryParse(match.Groups[1].Value, out int numberOfBooks))
			{
				return numberOfBooks;
			}
		}
		return -1; // Return -1 if parsing fails or element not found
	}

	/// <summary>
	/// Gets the total number of books from the Calibre content server at the specified URL.
	/// This is the primary public method.
	/// </summary>
	/// <param name="url">The URL of the Calibre mobile server page (e.g., http://localhost:8080/mobile).</param>
	/// <returns>The total number of books as an integer, or -1 if the operation fails.</returns>
	public async Task<int> GetTotalBooksAsync(string url)
	{
		var htmlContent = await GetHtmlContentAsync(url);
		if (htmlContent is null)
		{
			return -1; // Failed to fetch HTML
		}
		return ParseNumberOfBooksFromHtml(htmlContent);
	}

	/// <summary>
	/// Scrapes all books from the Calibre server and returns them as a list.
	/// </summary>
	/// <returns>A list of Book objects containing all metadata.</returns>
	public async Task<List<Book>> GetAllBooksAsync(string url, CancellationToken cancellationToken = default)
	{
		var allBooks = new List<Book>();
		await foreach (var book in GetBooksAsyncEnumerable(url, cancellationToken))
		{
			if(cancellationToken.IsCancellationRequested)
			{
				logger.Info("Scraping cancelled by user.");
				break; // Exit the loop if cancellation is requested
			}
			allBooks.Add(book);
		}
		return allBooks;
	}

	/// <summary>
	/// Scrapes books from the Calibre server page by page, yielding each book as it's found.
	/// This allows for faster data display as books are processed incrementally.
	/// </summary>
	/// <returns>An async enumerable of Book objects.</returns>
	
	public async IAsyncEnumerable<Book> GetBooksAsyncEnumerable(string url, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		string? currentPageUrl = "/mobile";
		int pageCount = 0;

#if WINDOWS
		var prefix = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Scheme) : string.Empty;
		if(string.IsNullOrEmpty(prefix) && prefix == "https" && !await NetworkChecker.ValidateSSLCerticate(url))
		{
			logger.Error($"Invalid URI scheme {prefix} provided. Please ensure the URL uses SSL");
			yield break; // Exit if the URL is invalid
		}
#endif
		logger.Info("Starting scrape of Calibre server...");

		while (!string.IsNullOrEmpty(currentPageUrl))
		{
			pageCount++;
			var fullUrl = url.Replace("/mobile", "") + currentPageUrl;
			logger.Info($"Scraping page {pageCount}: {fullUrl}");
			if(cancellationToken.IsCancellationRequested)
			{
				logger.Info("Scraping cancelled by user.");
				
				// Exit the loop if cancellation is requested
				yield break; 
			}
			// Download the HTML from the current page
			string htmlContent;
			try
			{
				htmlContent = await httpClient.GetStringAsync(fullUrl, cancellationToken);
			}
			catch (HttpRequestException e)
			{
				logger.Error($"Error fetching URL: {fullUrl}");
				logger.Error($"Message: {e.Message}");
				logger.Error("Please ensure your Calibre server is running and accessible.");
				yield break;
			}
			
			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(htmlContent);

			// Select all table rows from the main listing table
			var bookNodes = htmlDoc.DocumentNode.SelectNodes("//table[@id='listing']/tr");

			if (bookNodes is not null)
			{
				foreach (var node in bookNodes)
				{
					var book = ParseBookFromNode(url, node);
					if (book is not null)
					{
						yield return book; // Return each book immediately as it's parsed
					}
				}
			}

			// Find the "Next" link to determine the next page to scrape
			var nextLinkNode = htmlDoc.DocumentNode.SelectSingleNode("//a[text()='Next']");
			if (nextLinkNode is not null)
			{
				var rawHref = nextLinkNode.GetAttributeValue("href", "");
				// Decode the HTML entities (like &amp;) in the URL to get a valid request URI
				currentPageUrl = HtmlEntity.DeEntitize(rawHref);
			}
			else
			{
				// No "Next" link found on the page, so this is the last page.
				currentPageUrl = null;
			}
			if (cancellationToken.IsCancellationRequested)
			{
				logger.Info("Scraping cancelled by user.");
				yield break; // Exit the loop if cancellation is requested
			}
			try
			{
				// A small delay to be polite to the server
				await Task.Delay(delayBetweenRequests, cancellationToken);
			}
			catch (OperationCanceledException ex)
			{
				logger.Warn($"Error processing page {pageCount}: {ex.Message}");
				yield break; // Exit if there's an error processing the page
			}
			
		}

		logger.Info($"\nScraping complete. Processed {pageCount} pages.");
		yield break; // End of the async enumerable
	}

	static Book? ParseBookFromNode(string url, HtmlNode node)
	{
		var book = new Book();
		url = url.Replace("/mobile", "");
		var thumbnailNode = node.SelectSingleNode(".//img[@class='thumbnail']");
		book.ThumbnailUrl = url + thumbnailNode?.GetAttributeValue("src", "");

		var downloadNode = node.SelectSingleNode(".//a[contains(@href, '/legacy/get/')]");
		book.DownloadUrl = url + downloadNode?.GetAttributeValue("href", "");

		var firstLineNode = node.SelectSingleNode(".//span[@class='first-line']");
		if (firstLineNode is not null)
		{
			var text = HtmlEntity.DeEntitize(firstLineNode.InnerText)?.Trim();
			var parts = text?.Split([" by "], StringSplitOptions.RemoveEmptyEntries);
			if (parts?.Length > 1)
			{
				book.Title = string.Join(" by ", parts.Take(parts.Length - 1)).Trim();
				book.Author = parts[^1].Trim();
			}
			else
			{
				book.Title = text ?? string.Empty;
				book.Author = "Unknown";
			}
		}

		var secondLineNode = node.SelectSingleNode(".//span[@class='second-line']");
		if(secondLineNode?.InnerText is not null)
		{
			book.Date = HtmlEntity.DeEntitize(secondLineNode.InnerText.Trim()) ?? string.Empty;
		}

		return book;
	}
	public void Dispose()
	{
		Dispose(true);
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
			httpClient?.Dispose();
		}

		disposed = true;
	}

	
	[GeneratedRegex(@"of (\d+)", RegexOptions.None, 2000)]
	private static partial Regex MatchNumber();
}
