using System.Text.Json;

namespace SqlDummySeeder.Models;

public class RuleConfig
{
    public Dictionary<string, TableRule> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static RuleConfig Parse(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var root = System.Text.Json.JsonSerializer.Deserialize<RuleConfig>(json, options);
        if (root == null) throw new InvalidOperationException("Không thể parse JSON.");
        return root;
    }

    public ColumnRule? TryGet(string schema, string table, string column)
    {
        var key = $"{schema}.{table}";
        if (Tables.TryGetValue(key, out var tr) && tr.Columns.TryGetValue(column, out var colRule))
        {
            return colRule;
        }
        return null;
    }

    public int? TryGetCount(string schema, string table)
    {
        var key = $"{schema}.{table}";
        if (Tables.TryGetValue(key, out var tr))
        {
            return tr.Count;
        }
        return null;
    }
}

public class TableRule
{
    public int? Count { get; set; }
    public Dictionary<string, ColumnRule> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ColumnRule
{
    public string? Value { get; set; }
    public string? Type { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    public int? Scale { get; set; }
    public int? MaxLength { get; set; }
}
