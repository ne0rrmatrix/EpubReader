﻿namespace EpubReader.Models;

public class Image
{
	public string FileName { get; set; } = string.Empty;
	public byte[] Content { get; set; } = [];
}
