namespace EpubReader.Service;

public sealed class ReaderSettingsChangedEventArgs : EventArgs
{
	public ReaderSettingsChangedEventArgs(SettingsChangeKind changeKind)
	{
		ChangeKind = changeKind;
	}

	public SettingsChangeKind ChangeKind { get; }

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
