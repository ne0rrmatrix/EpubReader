namespace EpubReader.Messages;

public class ColorMessage(string backgroundColor, string textColor)
{
    public string BackgroundColor { get; } = backgroundColor;
    public string TextColor { get; } = textColor;
}
