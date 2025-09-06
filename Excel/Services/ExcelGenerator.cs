using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using SqlDummySeeder.Excel.Models;

namespace SqlDummySeeder.Excel.Services;

public class ExcelGenerator : IExcelGenerator
{
    public Task<byte[]> GenerateAsync(Template template, int rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Data");

        var orderedCols = template.Columns.OrderBy(x => x.Order).ToList();

        for (int c = 0; c < orderedCols.Count; c++)
        {
            ws.Cell(1, c + 1).Value = orderedCols[c].Name;
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var rnd = new Random();

        for (int r = 0; r < rows; r++)
        {
            int excelRow = r + 2;
            for (int c = 0; c < orderedCols.Count; c++)
            {
                var col = orderedCols[c];
                string value = col.Mode switch
                {
                    ColumnValueMode.FromList => ValueFromList(col, r, rnd),
                    ColumnValueMode.FormatString => ValueFromFormat(col.FormatString ?? "", r, rnd),
                    _ => ""
                };
                ws.Cell(excelRow, c + 1).Value = value;
            }
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    private static string ValueFromList(ColumnDefinition col, int rowIndex, Random rnd)
    {
        var items = col.AsListItems().ToList();
        if (items.Count == 0) return string.Empty;
        if (col.ListPickRandom)
        {
            return items[rnd.Next(items.Count)];
        }
        else
        {
            return items[rowIndex % items.Count];
        }
    }

    private static string ValueFromFormat(string format, int rowIndex, Random rnd)
    {
        string s = format;

        s = Regex.Replace(s, @"\{i(?::([^}]+))?\}", m =>
        {
            var idx = rowIndex + 1;
            var fmt = m.Groups[1].Success ? m.Groups[1].Value : null;
            return fmt is null ? idx.ToString() : idx.ToString(fmt, CultureInfo.InvariantCulture);
        });

        s = s.Replace("{guid}", Guid.NewGuid().ToString());

        s = Regex.Replace(s, @"\{date:(?<fmt>[^}]+)\}", m =>
        {
            var fmt = m.Groups["fmt"].Value;
            return DateTime.Today.ToString(fmt, CultureInfo.InvariantCulture);
        });

        s = Regex.Replace(s, @"\{now:(?<fmt>[^}]+)\}", m =>
        {
            var fmt = m.Groups["fmt"].Value;
            return DateTime.Now.ToString(fmt, CultureInfo.InvariantCulture);
        });

        s = Regex.Replace(s, @"\{rand:(?<min>-?\d+)-(?<max>-?\d+)\}", m =>
        {
            int min = int.Parse(m.Groups["min"].Value);
            int max = int.Parse(m.Groups["max"].Value);
            if (min > max) (min, max) = (max, min);
            return rnd.Next(min, max + 1).ToString(CultureInfo.InvariantCulture);
        });

        s = Regex.Replace(s, @"\{pick:(?<opts>[^}]+)\}", m =>
        {
            var opts = m.Groups["opts"].Value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (opts.Length == 0) return "";
            return opts[rnd.Next(opts.Length)];
        });

        return s;
    }
}
