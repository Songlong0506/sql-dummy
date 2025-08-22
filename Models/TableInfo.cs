namespace SqlDummySeeder.Models;

public record TableInfo(string SchemaName, string TableName)
{
    public override string ToString() => $"{SchemaName}.{TableName}";
}
