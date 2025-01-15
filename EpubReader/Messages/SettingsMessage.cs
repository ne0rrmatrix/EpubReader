using CommunityToolkit.Mvvm.Messaging.Messages;
using EpubReader.Models;

namespace EpubReader.Messages;
public class SettingsMessage(bool value) : ValueChangedMessage<bool>(value)
{
}
