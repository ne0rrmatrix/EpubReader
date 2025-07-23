namespace EpubReader.Models;

/// <summary>
/// Represents a selectable OPDS feed entry for navigation and filtering.
/// </summary>
public class OpdsFeedSelection
{
    /// <summary>
    /// Gets or sets the display title of the feed selection.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what this feed contains.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL to navigate to for this feed.
    /// </summary>
    public string FeedUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier for this feed selection.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this feed selection is currently selected.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets or sets the type of feed (e.g., "navcatalog", "library", "search").
    /// </summary>
    public string FeedType { get; set; } = string.Empty;
}