namespace SqlDummySeeder.Excel.Models;

public class TemplateEditViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<ColumnEditViewModel> Columns { get; set; } = new();
}

public class ColumnEditViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ColumnValueMode Mode { get; set; }
    public string? ListItemsRaw { get; set; }
    public bool ListPickRandom { get; set; }
    public string? FormatString { get; set; }
}
