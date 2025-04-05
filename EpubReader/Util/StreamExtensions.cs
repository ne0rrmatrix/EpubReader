using System.Text;
using System.Text.RegularExpressions;
using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using Microsoft.Maui.Storage;
using static System.Net.Mime.MediaTypeNames;
using Application = Microsoft.Maui.Controls.Application;

namespace EpubReader.Util;
public class StreamExtensions
{
	public Book? Book { get; private set; }
	public static StreamExtensions? Instance { get; private set; }
	public StreamExtensions()
	{
		Instance ??= this;
	}
	public void SetBook(Book book)
	{
		Book = book;
	}
	public string? Content(string fileName)
	{
		if (Instance is null || Book is null)
		{
			return null;
		}
		fileName = Path.GetFileName(fileName);
		return Book.Chapters.Find(f => f.FileName.Contains(fileName))?.HtmlFile ??
			Book.Files.FirstOrDefault(f => f.FileName.Contains(fileName))?.HTMLContent
			?? Book.Css.ToList().Find(f => f.FileName.Contains(fileName))?.Content
			?? Book.Files.ToList().Find(f => f.FileName.Contains(fileName))?.HTMLContent;
	}
	public byte[]? ByteContent(string fileName)
	{
		if (Instance is null || Book is null)
		{
			return null;
		}
		fileName = Path.GetFileName(fileName);
		return Book.Images.ToList().Find(f => f.FileName.Contains(fileName))?.Content
			?? Book.Fonts.ToList().Find(f => f.FileName.Contains(fileName))?.Content
		   ?? Book.Files.ToList().Find(f => f.FileName.Contains(fileName))?.Content;
	}

	public static Stream GetStream(string txt)
	{
		UTF8Encoding utfEncoding = new();
		byte[] postData = utfEncoding.GetBytes(
			txt);
		MemoryStream postDataStream = new(txt.Length);
		postDataStream.Write(postData, 0, postData.Length);
		postDataStream.Seek(0, SeekOrigin.Begin);
		return postDataStream;
	}

	public static Stream GetStream(byte[] bytes)
	{

		MemoryStream postDataStream = new(bytes.Length);
		postDataStream.Write(bytes, 0, bytes.Length);
		postDataStream.Seek(0, SeekOrigin.Begin);
		return postDataStream;
	}

	public static string GetMimeType(string fileName)
	{
		var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

		return extension switch
		{
			".xhtml" => "application/xhtml+xml",
			".html" or ".htm" => "text/html",
			".css" => "text/css",
			".ico" => "image/x-icon",
			".js" => "application/javascript",
			".json" => "application/json",
			".png" => "image/png",
			".jpg" or ".jpeg" => "image/jpeg",
			".gif" => "image/gif",
			".svg" => "image/svg+xml",
			".pdf" => "application/pdf",
			".txt" => "text/plain",
			".xml" => "application/xml",
			".zip" => "application/zip",
			".rar" => "application/x-rar-compressed",
			".7z" => "application/x-7z-compressed",
			".TTF" => "font/ttf",
			".tar" => "application/x-tar",
			".ttf" => "font/ttf",
			".woff" => "font/woff",
			".woff2" => "font/woff2",
			".eot" => "application/vnd.ms-fontobject",
			".otf" => "font/otf",
			_ => "application/octet-stream"
		};
	}

	public static bool IsText(string fileName)
	{
		var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
		return extension switch
		{
			".xhtml" => true,
			".txt" => true,
			".html" => true,
			".htm" => true,
			".css" => true,
			".js" => true,
			".json" => true,
			_ => false,
		};
	}
	public static bool IsBinary(string fileName)
	{
		var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
		return extension switch
		{
			".png" => true,
			".jpg" => true,
			".jpeg" => true,
			".gif" => true,
			".svg" => true,
			".otf" => true,
			".ttf" => true,
			".woff" => true,
			".woff2" => true,
			".pdf" => true,
			".ico" => true,
			_ => false,
		};
	}
}