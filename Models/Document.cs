using SQLite;

namespace iPDesktop.Models;

[Table("documents")]
public class Document
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull, MaxLength(260)]
    public string FileName { get; set; } = "";

    [MaxLength(20)]
    public string FileType { get; set; } = "";

    public long FileSizeBytes { get; set; }

    [NotNull]
    public string StoragePath { get; set; } = "";

    [Indexed]
    public DateTime UploadedAt { get; set; }

    public string SizeDisplay => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{FileSizeBytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
