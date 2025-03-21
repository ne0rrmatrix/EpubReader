using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EpubReader.Message;
public class JavaScriptMessage(string message) : ValueChangedMessage<string>(message)
{
}
