using SQLite;

namespace iPDesktop.Models;

[Table("users")]
public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = "";

    public string Email { get; set; } = "";
}
