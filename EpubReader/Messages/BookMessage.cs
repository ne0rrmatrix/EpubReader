using CommunityToolkit.Mvvm.Messaging.Messages;
using EpubReader.Models;

namespace EpubReader.Messages;
public class BookMessage(Book value) : ValueChangedMessage<Book>(value)
{
}
