namespace EpubReader.Util;
public static class IsImageExtension
{
	static readonly List<string> jpg = ["FF", "D8"];
	static readonly List<string> bmp = ["42", "4D"];
	static readonly List<string> gif = ["47", "49", "46"];
	static readonly List<string> png = ["89", "50", "4E", "47", "0D", "0A", "1A", "0A"];

	public enum ImageType
	{
		JPG,
		BMP,
		GIF,
		PNG,
		NONE
	}

	const string jPG = "FF";
	const string bMP = "42";
	const string gIF = "47";
	const string pNG = "89";

	public static bool IsImage(this string file, out ImageType type)
	{
		type = ImageType.NONE;
		if (string.IsNullOrWhiteSpace(file))
		{
			return false;
		}

		if (!File.Exists(file))
		{
			return false;
		}

		using var stream = File.OpenRead(file);
		return stream.IsImage(out type);
	}

	public static bool IsImage(this Stream stream, out ImageType type)
	{
		type = ImageType.NONE;
		stream.Seek(0, SeekOrigin.Begin);
		string bit = stream.ReadByte().ToString("X2");
		switch (bit)
		{
			case jPG:
				if (stream.IsImage(jpg))
				{
					type = ImageType.JPG;
					return true;
				}
				break;
			case bMP:
				if (stream.IsImage(bmp))
				{
					type = ImageType.BMP;
					return true;
				}
				break;
			case gIF:
				if (stream.IsImage(gif))
				{
					type = ImageType.GIF;
					return true;
				}
				break;
			case pNG:
				if (stream.IsImage(png))
				{
					type = ImageType.PNG;
					return true;
				}
				break;
			default:
				break;
		}
		return false;
	}

	public static bool IsImage(this Stream stream, List<string> comparer)
	{
		stream.Seek(0, SeekOrigin.Begin);
		foreach (string c in comparer)
		{
			string bit = stream.ReadByte().ToString("X2");
			if (0 != string.Compare(bit, c))
			{
				return false;
			}
		}
		return true;
	}
}
