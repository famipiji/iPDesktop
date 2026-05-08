namespace iPDesktop.Models;

public class ProfileClass
{
    public string Id   { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Document";
    public string Mode { get; set; } = "Offline";
    public List<string> Properties { get; set; } = [];
}
