using SQLite;

namespace EpubReader.Models;

[Table("FileData")]
public class FileData
{
    [PrimaryKey, AutoIncrement, Column("Id")]
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
