namespace EpubReader.Messages;

public class FontMessage(string fontFamily)
{
    public string FontFamily { get; } = fontFamily;
}
