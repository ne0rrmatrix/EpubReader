namespace EpubReader.Service;

public sealed class ReaderSettingsChangedEventArgs(SettingsChangeKind changeKind) : EventArgs
{
	public SettingsChangeKind ChangeKind { get; } = changeKind;

	public bool RequiresPaginationRefresh => ChangeKind is SettingsChangeKind.FontSize
		or SettingsChangeKind.FontFamily
		or SettingsChangeKind.Layout
		or SettingsChangeKind.Reset
		or SettingsChangeKind.LineSpacing
		or SettingsChangeKind.TextAlignment
		or SettingsChangeKind.ParagraphSpacing
		or SettingsChangeKind.Hyphenation
		or SettingsChangeKind.LetterSpacing
		or SettingsChangeKind.WordSpacing;
}