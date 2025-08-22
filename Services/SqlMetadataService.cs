using Dapper;
using SqlDummySeeder.Models;

namespace SqlDummySeeder.Services;

public class SqlMetadataService
{
    private readonly SqlConnectionFactory _factory;

    public SqlMetadataService(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<string>> GetDatabasesAsync(ConnectionInput input, bool includeSystem = false)
    {
        await using var conn = _factory.CreateForServer(input);
        var sql = includeSystem
            ? "SELECT name FROM sys.databases ORDER BY name"
            : "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name";
        var rows = await conn.QueryAsync<string>(sql);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(ConnectionInput input)
    {
        await using var conn = _factory.Create(input);
        var sql = @"
SELECT TABLE_SCHEMA as SchemaName, TABLE_NAME as TableName
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE='BASE TABLE'
ORDER BY TABLE_SCHEMA, TABLE_NAME";
        var rows = await conn.QueryAsync<TableInfo>(sql);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(ConnectionInput connInfo, string schema, string table)
    {
        await using var conn = _factory.Create(connInfo);
        var sql = @"
SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.is_nullable AS IsNullable,
    COLUMNPROPERTY(c.object_id, c.name, 'IsIdentity') AS IsIdentity,
    c.is_computed AS IsComputed,
    CASE WHEN ty.name IN ('timestamp','rowversion') THEN 1 ELSE 0 END AS IsRowVersion,
    c.max_length AS MaxLength,
    c.precision AS NumericPrecision,
    c.scale AS NumericScale
FROM sys.schemas s
JOIN sys.tables t ON t.schema_id = s.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE s.name = @schema AND t.name = @table
ORDER BY c.column_id";

        var rows = await conn.QueryAsync<ColumnInfo>(sql, new { schema, table });

        foreach (var r in rows)
        {
            var t = r.DataType?.ToLowerInvariant();

            if (r.MaxLength == -1)
            {
                r.MaxLength = null;
            }

            if (t == "nvarchar" || t == "nchar")
            {
                if (r.MaxLength.HasValue && r.MaxLength.Value > 0)
                {
                    r.MaxLength = r.MaxLength.Value / 2;
                }
            }
        }

        return rows.ToList();
    }

    public async Task<IReadOnlyList<ForeignKeyEdge>> GetForeignKeysAsync(ConnectionInput input)
    {
        await using var conn = _factory.Create(input);
        var sql = @"
SELECT 
    fk.name AS ConstraintName,
    schP.name AS ParentSchema, tP.name AS ParentTable, cP.name AS ParentColumn,
    schC.name AS ChildSchema,  tC.name AS ChildTable,  cC.name AS ChildColumn,
    fkc.constraint_column_id AS KeyOrdinal
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables tP ON fk.referenced_object_id = tP.object_id
JOIN sys.schemas schP ON tP.schema_id = schP.schema_id
JOIN sys.columns cP ON cP.object_id = tP.object_id AND cP.column_id = fkc.referenced_column_id
JOIN sys.tables tC ON fk.parent_object_id = tC.object_id
JOIN sys.schemas schC ON tC.schema_id = schC.schema_id
JOIN sys.columns cC ON cC.object_id = tC.object_id AND cC.column_id = fkc.parent_column_id
ORDER BY fk.name, fkc.constraint_column_id";
        var rows = await conn.QueryAsync<ForeignKeyEdge>(sql);
        return rows.ToList();
    }

    public List<TableInfo> OrderByDependencies(IEnumerable<TableInfo> selected, IEnumerable<ForeignKeyEdge> allEdges)
    {
        var selectedSet = new HashSet<string>(selected.Select(s => $"{s.SchemaName}.{s.TableName}"), StringComparer.OrdinalIgnoreCase);
        var nodes = selectedSet.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var edges = new List<(string child, string parent)>();
        foreach (var e in allEdges)
        {
            var childKey = $"{e.ChildSchema}.{e.ChildTable}";
            var parentKey = $"{e.ParentSchema}.{e.ParentTable}";
            if (selectedSet.Contains(childKey) && selectedSet.Contains(parentKey))
            {
                edges.Add((childKey, parentKey));
            }
        }

        var indeg = nodes.ToDictionary(n => n, n => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var (child, parent) in edges)
        {
            indeg[child] = indeg.GetValueOrDefault(child) + 1;
        }

        var q = new Queue<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var ordered = new List<String>();

        while (q.Count > 0)
        {
            var n = q.Dequeue();
            ordered.Add(n);
            foreach (var (child, parent) in edges.Where(e => e.parent.Equals(n, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                indeg[child] -= 1;
                if (indeg[child] == 0) q.Enqueue(child);
            }
        }

        foreach (var n in nodes)
        {
            if (!ordered.Contains(n, StringComparer.OrdinalIgnoreCase))
                ordered.Add(n);
        }

        return ordered
            .Select(k => {
                var parts = k.Split('.', 2);
                return new TableInfo(parts[0], parts[1]);
            })
            .ToList();
    }
}
