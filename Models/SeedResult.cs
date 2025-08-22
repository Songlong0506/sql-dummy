namespace SqlDummySeeder.Models;

public class SeedResult
{
    public List<PerTableResult> Tables { get; set; } = new();
}

public class PerTableResult
{
    public string SchemaName { get; set; } = "";
    public string TableName { get; set; } = "";
    public int Inserted { get; set; }
    public string? Error { get; set; }
}
