namespace SqlDummySeeder.Excel.Models;

public enum ColumnValueMode
{
    FromList = 0,
    FormatString = 1
}

public class ColumnDefinition
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public Template? Template { get; set; }

    public int Order { get; set; }
    public string Name { get; set; } = "";
    public ColumnValueMode Mode { get; set; }

    public string? ListItemsRaw { get; set; }
    public bool ListPickRandom { get; set; } = false;

    public string? FormatString { get; set; }

    public IEnumerable<string> AsListItems() =>
        string.IsNullOrWhiteSpace(ListItemsRaw)
            ? Enumerable.Empty<string>()
            : ListItemsRaw!
                .Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
