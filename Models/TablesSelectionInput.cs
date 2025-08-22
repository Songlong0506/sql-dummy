using System.ComponentModel.DataAnnotations;

namespace SqlDummySeeder.Models;

public class TableSeedRequest
{
    public string SchemaName { get; set; } = "dbo";
    public string TableName { get; set; } = "";

    [Range(1, 100000)]
    public int Count { get; set; } = 10;

    public bool Selected { get; set; } = false;

    public override string ToString() => $"{SchemaName}.{TableName} x{Count} {(Selected ? "[x]" : "")}";
}

public class TablesSelectionInput
{
    public ConnectionInput Connection { get; set; } = new ConnectionInput();
    public List<TableSeedRequest> Tables { get; set; } = new();
    public string? RulesJson { get; set; }
}
