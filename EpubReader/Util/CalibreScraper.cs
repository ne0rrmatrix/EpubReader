using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using EpubReader.Models;
using HtmlAgilityPack;
using MetroLog;

namespace EpubReader.Util;
public partial class CalibreScraper(string baseUrl, int delayBetweenRequestsMs = 100) : IDisposable
{
	readonly string baseUrl = baseUrl.TrimEnd('/');
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
			Console.WriteLine($"HTTP Request Error: {httpEx.Message}");
			// Handle specific HTTP errors (e.g., 404 Not Found, 500 Internal Server Error)
			return null;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An unexpected error occurred while fetching HTML: {ex.Message}");
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

		if (navigationSpan != null)
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
	public async Task<List<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default)
	{
		var allBooks = new List<Book>();
		await foreach (var book in GetBooksAsyncEnumerable(cancellationToken))
		{
			allBooks.Add(book);
		}
		return allBooks;
	}

	/// <summary>
	/// Scrapes books from the Calibre server page by page, yielding each book as it's found.
	/// This allows for faster data display as books are processed incrementally.
	/// </summary>
	/// <returns>An async enumerable of Book objects.</returns>
	
	public async IAsyncEnumerable<Book> GetBooksAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		string? currentPageUrl = "/mobile";
		int pageCount = 0;

		Console.WriteLine("Starting scrape of Calibre server...");

		while (!string.IsNullOrEmpty(currentPageUrl))
		{
			pageCount++;
			var fullUrl = baseUrl + currentPageUrl;
			Console.WriteLine($"Scraping page {pageCount}: {fullUrl}");
			
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

			if (bookNodes != null)
			{
				foreach (var node in bookNodes)
				{
					var book = ParseBookFromNode(node);
					if (book is not null)
					{
						yield return book; // Return each book immediately as it's parsed
					}
				}
			}

			// Find the "Next" link to determine the next page to scrape
			var nextLinkNode = htmlDoc.DocumentNode.SelectSingleNode("//a[text()='Next']");
			if (nextLinkNode != null)
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

	Book? ParseBookFromNode(HtmlNode node)
	{
		var book = new Book();

		var thumbnailNode = node.SelectSingleNode(".//img[@class='thumbnail']");
		book.ThumbnailUrl = baseUrl + thumbnailNode?.GetAttributeValue("src", "");

		var downloadNode = node.SelectSingleNode(".//a[contains(@href, '/legacy/get/')]");
		book.DownloadUrl = baseUrl + downloadNode?.GetAttributeValue("href", "");

		var firstLineNode = node.SelectSingleNode(".//span[@class='first-line']");
		if (firstLineNode != null)
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
