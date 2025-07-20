using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using MetroLog;

namespace EpubReader.ODPS;

/// <summary>
/// Provides functionality to parse OPDS (Open Publication Distribution System) feeds from Calibre servers.
/// </summary>
/// <remarks>
/// The FeedReader class handles the parsing of Atom XML feeds that conform to the OPDS specification,
/// extracting feed metadata and entry information for book discovery and navigation.
/// </remarks>
public class FeedReader
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FeedReader));
	readonly HttpClient httpClient;

	public FeedReader(HttpClient? httpClient = null)
	{
		this.httpClient = httpClient ?? new HttpClient();
	}

	/// <summary>
	/// Asynchronously retrieves and parses an OPDS feed from the specified URL.
	/// </summary>
	/// <param name="url">The URL of the OPDS feed to retrieve and parse.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A parsed OpdsFeed object containing the feed metadata and entries.</returns>
	/// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
	/// <exception cref="XmlException">Thrown when the XML content cannot be parsed.</exception>
	public async Task<OpdsFeed> GetFeedAsync(string url, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		logger.Info($"Fetching OPDS feed from: {url}");

		try
		{
			var xmlContent = await httpClient.GetStringAsync(url, cancellationToken);
			return ParseFeed(xmlContent);
		}
		catch (HttpRequestException ex)
		{
			logger.Error($"HTTP error fetching feed from {url}: {ex.Message}");
			throw;
		}
		catch (XmlException ex)
		{
			logger.Error($"XML parsing error for feed from {url}: {ex.Message}");
			throw;
		}
		catch (Exception ex)
		{
			logger.Error($"Unexpected error fetching feed from {url}: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Parses OPDS feed XML content into an OpdsFeed object.
	/// </summary>
	/// <param name="xmlContent">The XML content to parse.</param>
	/// <returns>A parsed OpdsFeed object.</returns>
	/// <exception cref="XmlException">Thrown when the XML content cannot be parsed.</exception>
	public OpdsFeed ParseFeed(string xmlContent)
	{
		if (string.IsNullOrWhiteSpace(xmlContent))
		{
			throw new ArgumentException("XML content cannot be null or empty", nameof(xmlContent));
		}

		try
		{
			var doc = XDocument.Parse(xmlContent);
			return ParseFeedFromDocument(doc);
		}
		catch (XmlException ex)
		{
			logger.Error($"Error parsing XML content: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Parses an OPDS feed from an XDocument.
	/// </summary>
	/// <param name="doc">The XDocument containing the OPDS feed.</param>
	/// <returns>A parsed OpdsFeed object.</returns>
	OpdsFeed ParseFeedFromDocument(XDocument doc)
	{
		var root = doc.Root ?? throw new XmlException("Document root is null");

		// Define namespaces
		var atomNs = XNamespace.Get("http://www.w3.org/2005/Atom");
		var dcNs = XNamespace.Get("http://purl.org/dc/terms/");
		var opdsNs = XNamespace.Get("http://opds-spec.org/2010/catalog");

		var feed = new OpdsFeed();

		// Parse feed-level elements
		feed.Title = GetElementValue(root, atomNs + "title");
		feed.Subtitle = GetElementValue(root, atomNs + "subtitle");
		feed.Id = GetElementValue(root, atomNs + "id");
		feed.Icon = GetElementValue(root, atomNs + "icon");

		if (DateTime.TryParse(GetElementValue(root, atomNs + "updated"), new CultureInfo("en-US"), out DateTime updated))
		{
			feed.Updated = updated;
		}

		// Parse author
		var authorElement = root.Element(atomNs + "author");
		if (authorElement is not null)
		{
			feed.Author = new OpdsAuthor
			{
				Name = GetElementValue(authorElement, atomNs + "name"),
				Uri = GetElementValue(authorElement, atomNs + "uri")
			};
		}

		// Parse feed-level links
		feed.Links = ParseLinks(root.Elements(atomNs + "link")).ToList();

		// Parse entries
		feed.Entries = root.Elements(atomNs + "entry")
			.Select(ParseEntry)
			.Where(entry => entry is not null)
			.ToList()!;

		logger.Info($"Parsed OPDS feed: {feed.Title} with {feed.Entries.Count} entries");
		return feed;
	}

	/// <summary>
	/// Parses an individual entry element from the OPDS feed.
	/// </summary>
	/// <param name="entryElement">The entry XElement to parse.</param>
	/// <returns>A parsed OpdsEntry object or null if parsing fails.</returns>
	OpdsEntry? ParseEntry(XElement entryElement)
	{
		try
		{
			var atomNs = XNamespace.Get("http://www.w3.org/2005/Atom");
			var dcNs = XNamespace.Get("http://purl.org/dc/terms/");

			var entry = new OpdsEntry
			{
				Title = GetElementValue(entryElement, atomNs + "title"),
				Id = GetElementValue(entryElement, atomNs + "id"),
				Content = GetElementValue(entryElement, atomNs + "content")
			};

			if (DateTime.TryParse(GetElementValue(entryElement, atomNs + "updated"), new CultureInfo("en-US"), out var updated))
			{
				entry.Updated = updated;
			}

			// Parse entry links
			entry.Links = ParseLinks(entryElement.Elements(atomNs + "link")).ToList();

			// Parse authors
			entry.Authors = entryElement.Elements(atomNs + "author")
				.Select(authorElement => new OpdsAuthor
				{
					Name = GetElementValue(authorElement, atomNs + "name"),
					Uri = GetElementValue(authorElement, atomNs + "uri")
				})
				.Where(author => !string.IsNullOrEmpty(author.Name))
				.ToList();

			return entry;
		}
		catch (Exception ex)
		{
			logger.Warn($"Error parsing entry: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Parses link elements into OpdsLink objects.
	/// </summary>
	/// <param name="linkElements">The collection of link XElements to parse.</param>
	/// <returns>An enumerable of OpdsLink objects.</returns>
	static IEnumerable<OpdsLink> ParseLinks(IEnumerable<XElement> linkElements)
	{
		foreach (var linkElement in linkElements)
		{
			var link = new OpdsLink
			{
				Href = linkElement.Attribute("href")?.Value,
				Type = linkElement.Attribute("type")?.Value,
				Rel = linkElement.Attribute("rel")?.Value,
				Title = linkElement.Attribute("title")?.Value
			};

			if (!string.IsNullOrEmpty(link.Href))
			{
				yield return link;
			}
		}
	}

	/// <summary>
	/// Gets the text value of an XML element safely.
	/// </summary>
	/// <param name="parent">The parent element to search within.</param>
	/// <param name="elementName">The name of the element to find.</param>
	/// <returns>The text value of the element or null if not found.</returns>
	static string? GetElementValue(XElement parent, XName elementName)
	{
		return parent.Element(elementName)?.Value?.Trim();
	}

	/// <summary>
	/// Searches for books in the OPDS catalog using the provided search terms.
	/// </summary>
	/// <param name="baseUrl">The base URL of the OPDS server.</param>
	/// <param name="searchTerms">The search terms to use for finding books.</param>
	/// <param name="libraryId">The library ID to search within (optional).</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>An OpdsFeed containing search results.</returns>
	public async Task<OpdsFeed> SearchAsync(string baseUrl, string searchTerms, string? libraryId = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var searchUrl = $"{baseUrl.TrimEnd('/')}/opds/search/{Uri.EscapeDataString(searchTerms)}";

		if (!string.IsNullOrEmpty(libraryId))
		{
			searchUrl += $"?library_id={Uri.EscapeDataString(libraryId)}";
		}

		logger.Info($"Searching OPDS catalog with terms: {searchTerms}");
		return await GetFeedAsync(searchUrl, cancellationToken);
	}
}

/// <summary>
/// Represents an OPDS feed containing metadata and entries.
/// </summary>
public class OpdsFeed
{
	public string? Title { get; set; }
	public string? Subtitle { get; set; }
	public string? Id { get; set; }
	public string? Icon { get; set; }
	public DateTime? Updated { get; set; }
	public OpdsAuthor? Author { get; set; }
	public List<OpdsLink> Links { get; set; } = [];
	public List<OpdsEntry> Entries { get; set; } = [];
}

/// <summary>
/// Represents an individual entry in an OPDS feed.
/// </summary>
public class OpdsEntry
{
	public string? Title { get; set; }
	public string? Id { get; set; }
	public string? Content { get; set; }
	public DateTime? Updated { get; set; }
	public List<OpdsLink> Links { get; set; } = [];
	public List<OpdsAuthor> Authors { get; set; } = [];
}

/// <summary>
/// Represents a link in an OPDS feed or entry.
/// </summary>
public class OpdsLink
{
	public string? Href { get; set; }
	public string? Type { get; set; }
	public string? Rel { get; set; }
	public string? Title { get; set; }
}

/// <summary>
/// Represents an author in an OPDS feed or entry.
/// </summary>
public class OpdsAuthor
{
	public string? Name { get; set; }
	public string? Uri { get; set; }
}