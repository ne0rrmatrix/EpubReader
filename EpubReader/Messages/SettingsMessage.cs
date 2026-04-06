using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EpubReader.Messages;

public enum SettingsChangeKind
{
	Unknown = 0,
	Theme = 1,
	FontSize = 2,
	FontFamily = 3,
	Layout = 4,
	Reset = 5
}

/// <summary>
/// Represents a message indicating that a settings value has changed.
/// </summary>
/// <remarks>This message is used to notify subscribers about the type of settings change that occurred so expensive
/// refresh work can be skipped or deferred when it is not needed.</remarks>
/// <param name="value"></param>
public sealed class SettingsMessage(SettingsChangeKind value) : ValueChangedMessage<SettingsChangeKind>(value)
{
	public bool RequiresPaginationRefresh => Value is SettingsChangeKind.FontSize or SettingsChangeKind.FontFamily or SettingsChangeKind.Layout or SettingsChangeKind.Reset;
}