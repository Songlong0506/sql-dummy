using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlDummySeeder.Excel.Data;
using SqlDummySeeder.Excel.Models;
using SqlDummySeeder.Excel.Services;

namespace SqlDummySeeder.Excel.Controllers;

public class TemplatesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IExcelGenerator _excel;
    public TemplatesController(AppDbContext db, IExcelGenerator excel)
    {
        _db = db;
        _excel = excel;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _db.Templates.Include(t => t.Columns.OrderBy(c => c.Order)).ToListAsync();
        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _db.Templates.Add(new Template { Name = name.Trim() });
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var template = await _db.Templates.FindAsync(id);
        if (template is null) return NotFound();
        ViewBag.Columns = await _db.Columns.Where(c => c.TemplateId == id).OrderBy(c => c.Order).ToListAsync();
        return View(template);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Template input)
    {
        var entity = await _db.Templates.FindAsync(input.Id);
        if (entity is null) return NotFound();
        entity.Name = input.Name;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = entity.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddColumn(int templateId, string name, ColumnValueMode mode)
    {
        var t = await _db.Templates.FindAsync(templateId);
        if (t is null) return NotFound();

        var nextOrder = await _db.Columns
            .Where(x => x.TemplateId == templateId)
            .Select(x => (int?)x.Order)
            .MaxAsync() ?? 0;

        _db.Columns.Add(new ColumnDefinition
        {
            TemplateId = templateId,
            Name = name.Trim(),
            Order = nextOrder + 1,
            Mode = mode
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = templateId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int templateId, [FromForm] int[] orderedIds)
    {
        var cols = await _db.Columns
            .Where(c => c.TemplateId == templateId)
            .OrderBy(c => c.Order)
            .ToListAsync();

        var orderMap = orderedIds
            .Select((id, idx) => new { id, idx = idx + 1 })
            .ToDictionary(x => x.id, x => x.idx);

        foreach (var c in cols)
        {
            if (orderMap.TryGetValue(c.Id, out var newOrder))
                c.Order = newOrder;
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveList(int id, string? listItemsRaw, bool? listPickRandom)
    {
        var col = await _db.Columns.FindAsync(id);
        if (col is null) return NotFound();
        col.ListItemsRaw = (listItemsRaw ?? string.Empty).Replace("\r\n", "\n");
        col.ListPickRandom = listPickRandom == true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = col.TemplateId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFormat(int id, string? formatString)
    {
        var col = await _db.Columns.FindAsync(id);
        if (col is null) return NotFound();
        col.FormatString = formatString;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = col.TemplateId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteColumn(int id)
    {
        var col = await _db.Columns.FindAsync(id);
        if (col is null) return NotFound();
        var tid = col.TemplateId;
        _db.Columns.Remove(col);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = tid });
    }

    public async Task<IActionResult> Export(int id, int rows = 100)
    {
        var template = await _db.Templates
            .Include(t => t.Columns.OrderBy(c => c.Order))
            .FirstOrDefaultAsync(t => t.Id == id);
        if (template is null) return NotFound();

        rows = Math.Clamp(rows, 1, 100000);
        var content = await _excel.GenerateAsync(template, rows);
        var fileName = $"{template.Name}-{rows}.xlsx";
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
