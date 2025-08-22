namespace SqlDummySeeder.Models;

public class ConnectionInput
{
    public string ServerName { get; set; } = "";
    public string DatabaseName { get; set; } = "";

    // "Windows" or "Sql"
    public string AuthMode { get; set; } = "Windows";

    public string? UserName { get; set; }
    public string? Password { get; set; }
}
