namespace SqlDummySeeder.Models;

public class ForeignKeyEdge
{
    public string ConstraintName { get; set; } = "";
    public string ParentSchema { get; set; } = "";
    public string ParentTable { get; set; } = "";
    public string ParentColumn { get; set; } = "";
    public string ChildSchema { get; set; } = "";
    public string ChildTable { get; set; } = "";
    public string ChildColumn { get; set; } = "";
    public int KeyOrdinal { get; set; }
}
