namespace EpubReader.Messages;

public class FontSizeMessage(int fontSize)
{
    public int FontSize { get; } = fontSize;
}
