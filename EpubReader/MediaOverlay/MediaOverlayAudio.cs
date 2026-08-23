namespace EpubReader.MediaOverlay;

public sealed record MediaOverlayAudio(string Source, TimeSpan? ClipBegin, TimeSpan? ClipEnd);