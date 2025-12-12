namespace EpubReader.Models.MediaOverlays;

/// <summary>
/// Represents a parsed SMIL media overlay document.
/// </summary>
public sealed class MediaOverlayDocument
{
	public MediaOverlayDocument(string id, string href, MediaOverlaySequence body, IReadOnlyList<MediaOverlayParallel> flattenedNodes)
	{
		Id = id;
		Href = href;
		Body = body;
		FlattenedNodes = flattenedNodes;
	}

	public string Id { get; }

	public string Href { get; }

	public MediaOverlaySequence Body { get; }

	public IReadOnlyList<MediaOverlayParallel> FlattenedNodes { get; }

	public List<string> AssociatedContentDocuments { get; } = [];

	public void AddAssociatedDocument(string href)
	{
		if (string.IsNullOrEmpty(href) || AssociatedContentDocuments.Contains(href, StringComparer.OrdinalIgnoreCase))
		{
			return;
		}

		AssociatedContentDocuments.Add(href);
	}
}

/// <summary>
/// Base node for SMIL sequences and parallels.
/// </summary>
public abstract class MediaOverlayNode
{
	public string? Id { get; init; }

	public string? EpubType { get; init; }
}

public sealed class MediaOverlaySequence : MediaOverlayNode
{
	public string? TextReference { get; init; }

	public List<MediaOverlayNode> Children { get; } = [];
}

public sealed class MediaOverlayParallel : MediaOverlayNode
{
	public MediaOverlayText? Text { get; init; }

	public MediaOverlayAudio? Audio { get; init; }
}

public sealed record MediaOverlayText(string Source);

public sealed record MediaOverlayAudio(string Source, TimeSpan? ClipBegin, TimeSpan? ClipEnd);

public sealed class MediaOverlayAudioResource
{
	public string RelativePath { get; init; } = string.Empty;

	public string NormalizedPath { get; init; } = string.Empty;

	public byte[] Content { get; init; } = [];

	public string? ContentType { get; init; }
}

public sealed class MediaOverlayParseResult(IReadOnlyList<MediaOverlayDocument> documents, string? activeClass, string? playbackActiveClass, string? narrator, TimeSpan? duration)
{
	public static MediaOverlayParseResult Empty { get; } = new([], null, null, null, null);

	public IReadOnlyList<MediaOverlayDocument> Documents { get; } = documents;

	public string? ActiveClass { get; } = activeClass;

	public string? PlaybackActiveClass { get; } = playbackActiveClass;

	public string? Narrator { get; } = narrator;

	public TimeSpan? Duration { get; } = duration;
}

public static class MediaOverlayPathHelper
{
	public static string Normalize(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}

		var normalized = path.Replace('\\', '/').Trim();

		while (normalized.StartsWith("./", StringComparison.Ordinal))
		{
			normalized = normalized[2..];
		}

		return normalized.TrimStart('/');
	}

	public static (string path, string? fragmentId) SplitSource(string? source)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			return (string.Empty, null);
		}

		var parts = source.Split('#', 2);
		var fragment = parts.Length > 1 ? parts[1] : null;
		return (parts[0], string.IsNullOrWhiteSpace(fragment) ? null : fragment);
	}

	public static string ExtractFileName(string? path)
	{
		return string.IsNullOrWhiteSpace(path)
			? string.Empty
			: Path.GetFileName(Normalize(path));
	}

	public static bool PathsReferToSameFile(string? candidatePath, string? targetPath)
	{
		var candidate = Normalize(candidatePath);
		var target = Normalize(targetPath);

		if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(target))
		{
			return false;
		}

		if (string.Equals(candidate, target, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (candidate.EndsWith($"/{target}", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (target.EndsWith($"/{candidate}", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return false;
	}
}