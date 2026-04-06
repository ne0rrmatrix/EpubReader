namespace EpubReader.Models;

public enum ReaderBridgeAction
{
	Unknown = 0,
	Jump = 1,
	Next = 2,
	Prev = 3,
	Menu = 4,
	PageLoad = 5,
	CharacterPosition = 6,
	SectionChange = 7,
	MediaOverlayLog = 8,
	MediaOverlayToggle = 9,
	MediaOverlayPlay = 10,
	MediaOverlayPause = 11,
	MediaOverlayNext = 12,
	MediaOverlayPrev = 13,
	MediaOverlaySeek = 14,
	LayoutOverflow = 15
}

public static class ReaderBridgeActionParser
{
	public static ReaderBridgeAction Parse(string? actionName)
	{
		return actionName?.Trim().ToLowerInvariant() switch
		{
			"jump" => ReaderBridgeAction.Jump,
			"next" => ReaderBridgeAction.Next,
			"prev" => ReaderBridgeAction.Prev,
			"menu" => ReaderBridgeAction.Menu,
			"pageload" => ReaderBridgeAction.PageLoad,
			"characterposition" => ReaderBridgeAction.CharacterPosition,
			"sectionchange" => ReaderBridgeAction.SectionChange,
			"mediaoverlaylog" => ReaderBridgeAction.MediaOverlayLog,
			"mediaoverlaytoggle" => ReaderBridgeAction.MediaOverlayToggle,
			"mediaoverlayplay" => ReaderBridgeAction.MediaOverlayPlay,
			"mediaoverlaypause" => ReaderBridgeAction.MediaOverlayPause,
			"mediaoverlaynext" => ReaderBridgeAction.MediaOverlayNext,
			"mediaoverlayprev" => ReaderBridgeAction.MediaOverlayPrev,
			"mediaoverlayseek" => ReaderBridgeAction.MediaOverlaySeek,
			"layoutoverflow" => ReaderBridgeAction.LayoutOverflow,
			_ => ReaderBridgeAction.Unknown,
		};
	}
}