namespace EpubReader.Util;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Represents a JavaScript message sent from the WebView to the native layer and
/// provides helpers to parse it and convert it to the internal "runcsharp" URL format.
/// </summary>
public sealed class BookPageJsMessage
{
	public string Action { get; init; } = string.Empty;
	public string RawJson { get; init; } = string.Empty;

	public string? Href { get; init; }

	public int? Position { get; init; }
   public int? ChapterIndex { get; init; }
	public double? Seconds { get; init; }

	public bool? Enabled { get; init; }

	public string? Message { get; init; }
	public string? Reason { get; init; }

	public static bool TryParse(string json, [NotNullWhen(true)] out BookPageJsMessage? message)
	{
		message = null;
		if (string.IsNullOrWhiteSpace(json))
		{
			return false;
		}

		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (!root.TryGetProperty("action", out var actionElem))
			{
				return false;
			}

			var href = GetStringProperty(root, "href");

			// Convert HTTP to HTTPS if needed
			if (!string.IsNullOrEmpty(href) && href.StartsWith("http://"))
			{
				href = "https://" + href[7..];
			}

			message = new BookPageJsMessage
			{
				Action = actionElem.GetString() ?? string.Empty,
				RawJson = json,
				Href = href,
				Position = GetInt32Property(root, "position"),
				ChapterIndex = GetInt32Property(root, "chapterIndex"),
				Seconds = GetDoubleProperty(root, "seconds"),
				Enabled = GetBoolProperty(root, "enabled"),
				Message = GetStringProperty(root, "message"),
				Reason = GetStringProperty(root, "reason"),
			};
			return true;
		}
		catch
		{
			// parse failure, return false with null message
			message = null;
			return false;
		}
	}

	static string? GetStringProperty(JsonElement root, string name) =>
		root.TryGetProperty(name, out var elem) ? elem.GetString() : null;

	static int? GetInt32Property(JsonElement root, string name) =>
		root.TryGetProperty(name, out var elem) && elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var val)
			? val : null;

	static double? GetDoubleProperty(JsonElement root, string name) =>
		root.TryGetProperty(name, out var elem) && elem.ValueKind == JsonValueKind.Number && elem.TryGetDouble(out var val)
			? val : null;

	static bool? GetBoolProperty(JsonElement root, string name) =>
		root.TryGetProperty(name, out var elem) && (elem.ValueKind == JsonValueKind.True || elem.ValueKind == JsonValueKind.False)
			? elem.GetBoolean() : null;

	/// <summary>
	/// Converts the parsed message into the internal "runcsharp" URL used by the native handler.
	/// Returns null if no mapping is available.
	/// </summary>
	public string? ToRuncsharpUrl()
	{
		var action = Action?.ToLowerInvariant() ?? string.Empty;
		return action switch
		{
			"jump" => Href is not null ? $"https://runcsharp.jump?{Href}" : null,
			"next" => "https://runcsharp.next?true",
			"prev" => "https://runcsharp.prev?true",
			"menu" => "https://runcsharp.menu?true",
			"pageload" => "https://runcsharp.pageLoad?true",
			"characterposition" => Position.HasValue ? $"https://runcsharp.characterposition?{Position.Value}" : null,
          "sectionchange" => ChapterIndex.HasValue ? $"https://runcsharp.sectionchange?{ChapterIndex.Value}" : null,
			"mediaoverlaytoggle" => Enabled.HasValue ? $"https://runcsharp.mediaoverlaytoggle?{Enabled.Value}" : null,
			"mediaoverlayplay" => "https://runcsharp.mediaoverlayplay?true",
			"mediaoverlaypause" => "https://runcsharp.mediaoverlaypause?true",
			"mediaoverlaynext" => "https://runcsharp.mediaoverlaynext?true",
			"mediaoverlayprev" => "https://runcsharp.mediaoverlayprev?true",
			"mediaoverlaylog" => !string.IsNullOrEmpty(Message) ? $"https://runcsharp.mediaoverlaylog?{Uri.EscapeDataString(Message)}" : null,
			"mediaoverlayseek" => Seconds.HasValue ? $"https://runcsharp.mediaoverlayseek?{Seconds.Value.ToString(CultureInfo.InvariantCulture)}" : null,
			"layoutoverflow" => $"https://runcsharp.layoutoverflow?{Uri.EscapeDataString(RawJson)}",
			_ => null,
		};
	}
}