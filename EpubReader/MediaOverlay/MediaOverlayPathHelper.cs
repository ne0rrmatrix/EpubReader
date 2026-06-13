namespace EpubReader.MediaOverlay;

public static class MediaOverlayPathHelper
{
	public static string Normalize(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}

		string normalized = path.Replace('\\', '/').Trim();
		int queryIndex = normalized.IndexOf('?', StringComparison.Ordinal);
		if (queryIndex >= 0)
		{
			normalized = normalized[..queryIndex];
		}

		try
		{
			normalized = Uri.UnescapeDataString(normalized);
		}
		catch (UriFormatException)
		{
			// Keep the original path when the EPUB contains invalid escape sequences.
		}

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

		string[] parts = source.Split('#', 2);
		string? fragment = parts.Length > 1 ? parts[1] : null;
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
		string candidate = Normalize(candidatePath);
		string target = Normalize(targetPath);

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