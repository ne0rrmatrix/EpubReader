using CommunityToolkit.Mvvm.Messaging.Messages;
using EpubReader.Models;
using FileInfo = EpubReader.Models.FileInfo;

namespace EpubReader.Messages;
public class FileMessage(FileInfo folderInfo) :ValueChangedMessage<FileInfo>(folderInfo)
{
}
