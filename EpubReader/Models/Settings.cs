﻿using EpubReader.Service;
using SQLite;

namespace EpubReader.Models;

[Table("settings")]
public class Settings
{
	[PrimaryKey, AutoIncrement, Column("Id")]
	public int Id { get; set; }
	public string FontFamily { get; set; } = "Times New Roman";
	public int FontSize { get; set; } = 12;
	public string BackgroundColor { get; set; } = "#FFFFFF";
	public string TextColor { get; set; } = "#000000";
	public string ColorScheme { get; set; } = "Light";
}
