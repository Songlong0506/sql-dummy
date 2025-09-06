using SqlDummySeeder.Excel.Models;

namespace SqlDummySeeder.Excel.Services;

public interface IExcelGenerator
{
    Task<byte[]> GenerateAsync(Template template, int rows);
}
