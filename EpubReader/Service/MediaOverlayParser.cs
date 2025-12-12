using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using EpubReader.Models.MediaOverlays;
using VersOne.Epub;
using VersOne.Epub.Schema;

namespace EpubReader.Service;

/// <summary>
/// Parses SMIL media overlay documents and associated metadata from an EPUB package.
/// </summary>
public static class MediaOverlayParser
{
    const string mediaOverlayMimeType = "application/smil+xml";
    static readonly XNamespace smilNamespace = "http://www.w3.org/ns/SMIL";
    static readonly XNamespace epubNamespace = "http://www.idpf.org/2007/ops";

    public static async Task<MediaOverlayParseResult> ParseAsync(EpubBookRef book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);

        var manifestItems = book.Schema.Package.Manifest.Items ?? new List<EpubManifestItem>();
        var overlayDocuments = new Dictionary<string, MediaOverlayDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifestItem in manifestItems.Where(item => string.Equals(item.MediaType, mediaOverlayMimeType, StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var smilFile = FindLocalTextFile(book, manifestItem.Href);
            if (smilFile is null)
            {
                System.Diagnostics.Debug.WriteLine($"Media overlay SMIL file not found: {manifestItem.Href}");
                continue;
            }

            var content = await smilFile.ReadContentAsTextAsync().ConfigureAwait(false);
            var resolvedHref = MediaOverlayPathHelper.Normalize(smilFile.FilePath);
            var overlayDocument = ParseSmil(manifestItem.Id, resolvedHref, content);
            overlayDocuments[manifestItem.Id] = overlayDocument;
        }
        System.Diagnostics.Debug.WriteLine($"Parsed {overlayDocuments.Count} media overlay documents.");
        foreach (var manifestItem in manifestItems)
        {
            if (string.IsNullOrWhiteSpace(manifestItem.MediaOverlay))
            {
                continue;
            }

            if (overlayDocuments.TryGetValue(manifestItem.MediaOverlay, out var document))
            {
                document.AddAssociatedDocument(manifestItem.Href);
            }
        }

        var metaItems = book.Schema.Package.Metadata.MetaItems ?? new List<EpubMetadataMeta>();
        var activeClass = ResolveMeta(metaItems, "media:active-class");
        var playbackActiveClass = ResolveMeta(metaItems, "media:playback-active-class");
        var narrator = ResolveMeta(metaItems, "media:narrator");
        var duration = ParseClockValue(ResolveMeta(metaItems, "media:duration"));

        if (overlayDocuments.Count == 0 && activeClass is null && playbackActiveClass is null && narrator is null && duration is null)
        {
            return MediaOverlayParseResult.Empty;
        }

        return new MediaOverlayParseResult(overlayDocuments.Values.ToList(), activeClass, playbackActiveClass, narrator, duration);
    }

    static MediaOverlayDocument ParseSmil(string manifestId, string href, string content)
    {
        var document = XDocument.Parse(content);
        var bodyElement = document.Root?.Element(smilNamespace + "body") ?? throw new InvalidOperationException("Media overlay SMIL document is missing the <body> element.");
        var bodySequence = ParseSequence(bodyElement);
        var flattened = FlattenParallels(bodySequence).ToList();
        return new MediaOverlayDocument(manifestId, href, bodySequence, flattened);
    }

    static MediaOverlaySequence ParseSequence(XElement element)
    {
        var sequence = new MediaOverlaySequence
        {
            Id = (string?)element.Attribute("id"),
            EpubType = (string?)element.Attribute(epubNamespace + "type"),
            TextReference = (string?)element.Attribute(epubNamespace + "textref")
        };
       
        foreach (var child in element.Elements())
        {
            if (child.Name == smilNamespace + "seq")
            {
                sequence.Children.Add(ParseSequence(child));
            }
            else if (child.Name == smilNamespace + "par")
            {
                sequence.Children.Add(ParseParallel(child));
            }
        }
        return sequence;
    }

    static MediaOverlayParallel ParseParallel(XElement element)
    {
        return new MediaOverlayParallel
        {
            Id = (string?)element.Attribute("id"),
            EpubType = (string?)element.Attribute(epubNamespace + "type"),
            Text = ParseText(element.Element(smilNamespace + "text")),
            Audio = ParseAudio(element.Element(smilNamespace + "audio"))
        };
    }

    static MediaOverlayText? ParseText(XElement? textElement)
    {
        var src = textElement?.Attribute("src")?.Value;
        return string.IsNullOrWhiteSpace(src) ? null : new MediaOverlayText(src);
    }

    static MediaOverlayAudio? ParseAudio(XElement? audioElement)
    {
        if( audioElement is null)
        {
            return null;
        }
        var src = audioElement.Attribute("src")?.Value;
        if (string.IsNullOrWhiteSpace(src))
        {
            return null;
        }
        var clipBegin = ParseClockValue(audioElement.Attribute("clipBegin")?.Value);
        var clipEnd = ParseClockValue(audioElement.Attribute("clipEnd")?.Value);
        return new MediaOverlayAudio(src, clipBegin, clipEnd);
    }

    static IEnumerable<MediaOverlayParallel> FlattenParallels(MediaOverlayNode node)
    {
        if (node is MediaOverlayParallel parallel)
        {
            yield return parallel;
            yield break;
        }

        if (node is not MediaOverlaySequence sequence)
        {
            yield break;
        }

        foreach (var child in sequence.Children)
        {
            foreach (var flattened in FlattenParallels(child))
            {
                yield return flattened;
            }
        }
    }

    static EpubLocalTextContentFileRef? FindLocalTextFile(EpubBookRef book, string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var normalizedTarget = MediaOverlayPathHelper.Normalize(href);
        return book.Content.AllFiles.Local
            .OfType<EpubLocalTextContentFileRef>()
            .FirstOrDefault(file => MediaOverlayPathHelper.PathsReferToSameFile(file.FilePath, normalizedTarget));
    }

    static string? ResolveMeta(IEnumerable<EpubMetadataMeta> metaItems, string property)
    {
        return metaItems.FirstOrDefault(meta => string.Equals(meta.Property, property, StringComparison.OrdinalIgnoreCase))?.Content;
    }

    static TimeSpan? ParseClockValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        // Try colon format (hh:mm:ss(.fraction) | mm:ss(.fraction) | ss(.fraction))
        if (TryParseColonTime(value, out TimeSpan ts))
        {
            return ts;
        }

        // Try unit format (e.g., "1.5s", "200ms", "2min", "1h")
        if (TryParseUnitTime(value, out ts))
        {
            return ts;
        }

        // Fallback: plain number = seconds
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double secondsFallback) &&
            !double.IsNaN(secondsFallback) && !double.IsInfinity(secondsFallback))
        {
            return TimeSpan.FromSeconds(secondsFallback);
        }

        return null;

        // Local helpers to keep cognitive complexity low
        static bool TryParseColonTime(string input, out TimeSpan result)
        {
            result = default;
            var parts = input.Split(':');
            if (parts.Length < 1 || parts.Length > 3)
            {
                return false;
            }

            if (!TryParseSeconds(parts, out double seconds))
            {
                return false;
            }

            int minutes = 0;
            int hours = 0;

            if (parts.Length >= 2 && !TryParseMinutes(parts, out minutes))
            {
                return false;
            }

            if (parts.Length == 3 && !TryParseHours(parts, out hours))
            {
                return false;
            }

            double totalSeconds = seconds + minutes * 60.0 + hours * 3600.0;
            if (double.IsNaN(totalSeconds) || double.IsInfinity(totalSeconds))
            {
                return false;
            }

            result = TimeSpan.FromSeconds(totalSeconds);
            return true;
        }

        static bool TryParseSeconds(string[] parts, out double seconds)
        {
            seconds = 0;
            return double.TryParse(parts[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
        }

        static bool TryParseMinutes(string[] parts, out int minutes)
        {
            minutes = 0;
            return int.TryParse(parts[^2], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes);
        }

        static bool TryParseHours(string[] parts, out int hours)
        {
            hours = 0;
            return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hours);
        }

        static bool TryParseUnitTime(string input, out TimeSpan result)
        {
            result = default;
            // units: ms, s, min, h (case-insensitive)
            var m = Regex.Match(input, @"^\s*(?<num>[-+]?\d+(\.\d+)?)\s*(?<unit>ms|s|min|h)\s*$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(2000));
            if (!m.Success)
            {
                return false;
            }

            var numStr = m.Groups["num"].Value;
            var unit = m.Groups["unit"].Value.ToLowerInvariant();

            if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return false;
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return false;
            }

            result = unit switch
            {
                "ms" => TimeSpan.FromMilliseconds(value),
                "s" => TimeSpan.FromSeconds(value),
                "min" => TimeSpan.FromMinutes(value),
                "h" => TimeSpan.FromHours(value),
                _ => default
            };

            // Ensure a valid non-default TimeSpan when unit matched
            const double epsilon = 1e-9;
            return m.Success && (result != default || Math.Abs(value) < epsilon);
        }
    }
}
