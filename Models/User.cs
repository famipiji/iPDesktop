using SQLite;

namespace iPDesktop.Models;

[Table("users")]
public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique, NotNull, MaxLength(100)]
    public string Username { get; set; } = "";

    [NotNull]
    public string Password { get; set; } = "";
}
