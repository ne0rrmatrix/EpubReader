namespace EpubReader.Models.MediaOverlays;

public sealed record MediaOverlayAudio(string Source, TimeSpan? ClipBegin, TimeSpan? ClipEnd);
