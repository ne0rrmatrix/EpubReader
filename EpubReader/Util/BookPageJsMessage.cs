namespace EpubReader.Util;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using EpubReader.Models;

/// <summary>
/// Represents a structured JavaScript message sent from the WebView to the native layer and
/// provides helpers to parse the bridge payload into a typed C# model.
/// </summary>
public sealed class BookPageJsMessage
{
	public ReaderBridgeAction Action { get; init; } = ReaderBridgeAction.Unknown;
	public string ActionName { get; init; } = string.Empty;
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

			var actionName = actionElem.GetString() ?? string.Empty;

			message = new BookPageJsMessage
			{
				Action = ReaderBridgeActionParser.Parse(actionName),
				ActionName = actionName,
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
}