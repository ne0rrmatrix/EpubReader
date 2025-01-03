using SQLite;

namespace EpubReader.Models;

[Table("FileData")]
public class FileData
{
    [PrimaryKey, AutoIncrement, Column("Id")]
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int NumberofChapters { get; set; } = 0;
    public string Author { get; set; } = string.Empty;
    public string CoverImageFileName { get; set; } = string.Empty;

}
