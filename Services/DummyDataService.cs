using System.Collections.Generic;
using System.Data;
using Bogus;
using Dapper;
using Microsoft.Data.SqlClient;
using SqlDummySeeder.Models;

namespace SqlDummySeeder.Services;

public class DummyDataService
{
    private static bool IsNumericType(string dt)
    {
        dt = dt.ToLowerInvariant();
        return dt is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real";
    }

    private static bool IsStringType(string dt)
    {
        dt = dt.ToLowerInvariant();
        return dt is "char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext" or "sysname";
    }

    private static bool IsBooleanType(string dt)
    {
        return string.Equals(dt, "bit", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDateTimeType(string dt)
    {
        dt = dt.ToLowerInvariant();
        return dt is "date" or "datetime" or "smalldatetime" or "datetime2";
    }

    private static bool IsBinaryType(string dt)
    {
        dt = dt.ToLowerInvariant();
        return dt is "binary" or "varbinary" or "image";
    }

    private static bool IsGuidType(string dt)
    {
        return string.Equals(dt, "uniqueidentifier", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTimeType(string dt)
    {
        return string.Equals(dt, "time", System.StringComparison.OrdinalIgnoreCase);
    }



    private readonly SqlConnectionFactory _factory;
    private readonly SqlMetadataService _meta;

    public DummyDataService(SqlConnectionFactory factory, SqlMetadataService meta)
    {
        _factory = factory;
        _meta = meta;
    }

    public async Task<SeedResult> InsertDummyForTablesAsync(ConnectionInput connInfo, List<TableSeedRequest> tables, RuleConfig? config)
    {
        var result = new SeedResult();

        var edges = await _meta.GetForeignKeysAsync(connInfo);
        var selectedInfos = tables.Select(t => new TableInfo(t.SchemaName, t.TableName)).ToList();
        var order = _meta.OrderByDependencies(selectedInfos, edges);

        var reqMap = tables.ToDictionary(t => $"{t.SchemaName}.{t.TableName}", t => t, StringComparer.OrdinalIgnoreCase);

        foreach (var ti in order)
        {
            var key = $"{ti.SchemaName}.{ti.TableName}";
            var req = reqMap[key];

            var count = config?.TryGetCount(ti.SchemaName, ti.TableName) ?? req.Count;
            try
            {
                var inserted = await InsertDummyAsync(connInfo, ti.SchemaName, ti.TableName, count, config);
                result.Tables.Add(new PerTableResult { SchemaName = ti.SchemaName, TableName = ti.TableName, Inserted = inserted });
            }
            catch (Exception ex)
            {
                result.Tables.Add(new PerTableResult { SchemaName = ti.SchemaName, TableName = ti.TableName, Inserted = 0, Error = ex.Message });
            }
        }

        return result;
    }

    public async Task<int> InsertDummyAsync(ConnectionInput connInfo, string schema, string table, int count, RuleConfig? config)
    {
        var columns = (await _meta.GetColumnsAsync(connInfo, schema, table))
            .Where(c => !c.IsIdentity && !c.IsComputed && !c.IsRowVersion)
            .ToList();

        if (columns.Count == 0 || count <= 0) return 0;

        await using var conn = _factory.Create(connInfo);
        await conn.OpenAsync();

        var fkLookups = await LoadForeignKeyLookupsAsync(conn, connInfo, schema, table, columns);

        using var dt = new DataTable();
        foreach (var col in columns)
        {
            var type = MapToClrType(col);
            dt.Columns.Add(col.ColumnName, Nullable.GetUnderlyingType(type) ?? type);
        }

        var faker = new Faker("en");
        for (int i = 0; i < count; i++)
        {
            var row = dt.NewRow();
            foreach (var col in columns)
            {
                if (fkLookups.TryGetValue(col.ColumnName, out var values))
                {
                    if (values.Count == 0)
                    {
                        if (col.IsNullable) { row[col.ColumnName] = DBNull.Value; continue; }
                        throw new InvalidOperationException($"Cột FK [{col.ColumnName}] yêu cầu dữ liệu từ bảng cha nhưng không có giá trị nào. Hãy seed bảng cha trước.");
                    }
                    row[col.ColumnName] = values[faker.Random.Int(0, values.Count - 1)];
                    continue;
                }

                var rule = config?.TryGet(schema, table, col.ColumnName);
                row[col.ColumnName] = GenerateValue(faker, col, rule) ?? DBNull.Value;
            }
            dt.Rows.Add(row);
        }

        using var bulk = new SqlBulkCopy(conn)
        {
            DestinationTableName = $"[{schema}].[{table}]",
            BatchSize = Math.Min(count, 5000),
            EnableStreaming = true
        };
        foreach (var col in columns)
        {
            bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        await bulk.WriteToServerAsync(dt);
        return dt.Rows.Count;
    }

    private static Type MapToClrType(ColumnInfo col)
    {
        var t = col.DataType.ToLowerInvariant();
        return t switch
        {
            "bit" => typeof(bool),
            "tinyint" => typeof(byte),
            "smallint" => typeof(short),
            "int" => typeof(int),
            "bigint" => typeof(long),
            "decimal" or "numeric" => typeof(decimal),
            "float" => typeof(double),
            "real" => typeof(float),
            "money" or "smallmoney" => typeof(decimal),
            "date" or "datetime" or "smalldatetime" or "datetime2" => typeof(DateTime),
            "time" => typeof(TimeSpan),
            "uniqueidentifier" => typeof(Guid),
            "binary" or "varbinary" or "image" => typeof(byte[]),
            _ => typeof(string)
        };
    }


    private static object? GenerateValue(Faker faker, ColumnInfo col, ColumnRule? rule)
    {
        if (rule != null)
        {
            if (!string.IsNullOrWhiteSpace(rule.Value))
            {
                return CoerceValue(rule.Value!, col);
            }

            if (!string.IsNullOrWhiteSpace(rule.Type))
            {
                var t = rule.Type!.ToLowerInvariant();
                var min = rule.Min ?? 0;
                var max = rule.Max ?? 1000;
                var scale = rule.Scale ?? col.NumericScale ?? 2;
                var maxLen = rule.MaxLength ?? col.MaxLength ?? 100;

                switch (t)
                {
                    case "email": return faker.Internet.Email();
                    case "phone": return faker.Phone.PhoneNumber();
                    case "fullname": return faker.Name.FullName();
                    case "firstname": return faker.Name.FirstName();
                    case "lastname": return faker.Name.LastName();
                    case "username": return faker.Internet.UserName();
                    case "password": return faker.Internet.Password();
                    case "title": return faker.Lorem.Sentence(4).Truncate(maxLen);
                    case "sentence": return faker.Lorem.Sentence().Truncate(maxLen);
                    case "sentences": return faker.Lorem.Sentences(2).Truncate(maxLen);
                    case "paragraph": return faker.Lorem.Paragraph().Truncate(maxLen);
                    case "paragraphs": return faker.Lorem.Paragraphs(2).Truncate(maxLen);
                    case "city": return faker.Address.City();
                    case "country": return faker.Address.Country();
                    case "address": return faker.Address.FullAddress().Truncate(maxLen);
                    case "zip": return faker.Address.ZipCode();
                    case "guid": return Guid.NewGuid();
                    case "randomint": return faker.Random.Int(min, max);
                    case "randomdecimal": return Math.Round(faker.Random.Decimal(min, max <= min ? min + 1 : max), scale);
                    case "daterecentyears":
                        var years = Math.Max(1, max);
                        return faker.Date.Between(DateTime.UtcNow.AddYears(-years), DateTime.UtcNow);
                    case "bool": return faker.Random.Bool();
                    case "code":
                        return IsNumericType(col.DataType) ? (object)faker.Random.Int(0, 999999) :
                                          IsBooleanType(col.DataType) ? (object)faker.Random.Bool() :
                                          faker.Random.AlphaNumeric(Math.Min(maxLen, 12));
                }
            }
        }

        var name = col.ColumnName.ToLowerInvariant();
        var dt = col.DataType.ToLowerInvariant();

        // Always honor exact type families first
        if (IsGuidType(dt)) return Guid.NewGuid();
        if (IsBooleanType(dt)) return faker.Random.Bool();
        if (IsDateTimeType(dt)) return faker.Date.Between(DateTime.UtcNow.AddYears(-5), DateTime.UtcNow);
        if (IsTimeType(dt)) return faker.Date.Timespan(TimeSpan.FromHours(23));
        if (IsBinaryType(dt)) return faker.Random.Bytes(16);

        // STRING-TYPED columns: allow name-based heuristics
        if (IsStringType(dt))
        {
            if (name.Contains("email"))
                return faker.Internet.Email();
            if (name.Contains("phone") || name.Contains("tel"))
                return faker.Phone.PhoneNumber();
            if (name.Contains("first_name"))
                return faker.Name.FirstName();
            if (name.Contains("last_name"))
                return faker.Name.LastName();
            if (name.Contains("name"))
                return faker.Name.FullName();
            if (name.Contains("address"))
                return faker.Address.FullAddress().Truncate(col.MaxLength ?? 100);
            if (name.Contains("city"))
                return faker.Address.City();
            if (name.Contains("country"))
                return faker.Address.Country();
            if (name.Contains("zip") || name.Contains("postal"))
                return faker.Address.ZipCode();
            if (name.Contains("user") && name.Contains("name"))
                return faker.Internet.UserName();
            if (name.Contains("password"))
                return faker.Internet.Password();
            if (name.Contains("title"))
                return faker.Lorem.Sentence(4).Truncate(col.MaxLength ?? 100);
            if (name.Contains("desc") || name.Contains("notes"))
                return faker.Lorem.Sentences(2).Truncate(col.MaxLength ?? 100);
            if (name.Contains("code"))
                return faker.Random.AlphaNumeric(Math.Min(col.MaxLength ?? 10, 12));

            // default string
            return faker.Lorem.Word().Truncate(col.MaxLength ?? 100);
        }

        // NON-STRING columns: only a small heuristic for "code" when numeric
        if (name.Contains("code") && IsNumericType(dt))
            return faker.Random.Int(0, 999999);

        // Fallback by exact SQL type
        return dt switch
        {
            "tinyint" => faker.Random.Byte(),
            "smallint" => faker.Random.Short(0, short.MaxValue),
            "int" => faker.Random.Int(0, int.MaxValue / 4),
            "bigint" => faker.Random.Long(0, int.MaxValue),
            "decimal" or "numeric" => Math.Round(faker.Random.Decimal(0, 100000), col.NumericScale ?? 2),
            "money" or "smallmoney" => Math.Round(faker.Random.Decimal(0, 100000), 2),
            "float" => faker.Random.Double(),
            "real" => (float)faker.Random.Double(),
            _ => faker.Lorem.Word().Truncate(col.MaxLength ?? 100) // unexpected types -> string
        };
    }


    private static object CoerceValue(string raw, ColumnInfo col)
    {
        var t = col.DataType.ToLowerInvariant();
        try
        {
            return t switch
            {
                "bit" => bool.Parse(raw),
                "tinyint" => byte.Parse(raw),
                "smallint" => short.Parse(raw),
                "int" => int.Parse(raw),
                "bigint" => long.Parse(raw),
                "decimal" or "numeric" or "money" or "smallmoney" => decimal.Parse(raw),
                "float" => double.Parse(raw),
                "real" => float.Parse(raw),
                "date" or "datetime" or "smalldatetime" or "datetime2" => DateTime.Parse(raw),
                "time" => TimeSpan.Parse(raw),
                "uniqueidentifier" => Guid.Parse(raw),
                _ => raw.Truncate(col.MaxLength ?? 4000)
            };
        }
        catch
        {
            return raw.Truncate(col.MaxLength ?? 4000);
        }
    }

    private async Task<Dictionary<string, List<object>>> LoadForeignKeyLookupsAsync(SqlConnection conn, ConnectionInput connInfo, string schema, string table, List<ColumnInfo> columns)
    {
        var allEdges = await _meta.GetForeignKeysAsync(connInfo);
        var forThis = allEdges.Where(e => e.ChildSchema.Equals(schema, StringComparison.OrdinalIgnoreCase)
                                       && e.ChildTable.Equals(table, StringComparison.OrdinalIgnoreCase))
                              .ToList();

        var singleColumnFks = forThis
            .GroupBy(e => e.ConstraintName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .Select(g => g.First())
            .ToList();

        var lookup = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in singleColumnFks)
        {
            var childCol = fk.ChildColumn;
            var parentCol = fk.ParentColumn;
            var parentTable = $"[{fk.ParentSchema}].[{fk.ParentTable}]";
            var sql = $"SELECT DISTINCT TOP 1000 [{parentCol}] FROM {parentTable}";
            var values = (await conn.QueryAsync<object>(sql)).ToList();
            lookup[childCol] = values;
        }

        return lookup;
    }
}
static class StringExtensions
{
    public static string Truncate(this string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (maxLen <= 0) return value; // safely ignore non-positive
        return value.Length <= maxLen ? value : value.Substring(0, maxLen);
    }
}
