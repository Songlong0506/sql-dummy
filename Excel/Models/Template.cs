namespace SqlDummySeeder.Excel.Models;

public class Template
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public ICollection<ColumnDefinition> Columns { get; set; } = new List<ColumnDefinition>();
}
