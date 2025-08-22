namespace SqlDummySeeder.Models;

public class ColumnInfo
{
    public string SchemaName { get; set; } = "dbo";
    public string TableName { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsComputed { get; set; }
    public bool IsRowVersion { get; set; }
    public int? MaxLength { get; set; }
    public int? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
}
