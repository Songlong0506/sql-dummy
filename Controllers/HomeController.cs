using Microsoft.AspNetCore.Mvc;
using SqlDummySeeder.Models;
using SqlDummySeeder.Services;

namespace SqlDummySeeder.Controllers;

public class HomeController : Controller
{
    private readonly SqlMetadataService _meta;
    private readonly DummyDataService _dummy;
    private readonly SavedConnectionsService _saved;

    public HomeController(SqlMetadataService meta, DummyDataService dummy, SavedConnectionsService saved)
    {
        _meta = meta;
        _dummy = dummy;
        _saved = saved;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var model = new TablesSelectionInput
        {
            Connection = new ConnectionInput { AuthMode = "Windows" }
        };
        ViewBag.Saved = _saved.GetAll();
        return View(model);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ListDatabases([FromBody] ConnectionInput input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input.ServerName))
                return Json(new { ok = false, error = "Thiếu ServerName." });

            var dbs = await _meta.GetDatabasesAsync(input, includeSystem: false);
            return Json(new { ok = true, data = dbs });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ex.Message });
        }
    }

    // Connect and list tables on the same page, save/refresh saved connections
    [HttpPost]
    public async Task<IActionResult> Index(TablesSelectionInput model)
    {
        if (string.IsNullOrWhiteSpace(model.Connection.ServerName) || string.IsNullOrWhiteSpace(model.Connection.DatabaseName))
        {
            ModelState.AddModelError("", "Vui lòng nhập ServerName và chọn Database Name.");
            ViewBag.Saved = _saved.GetAll();
            return View(model);
        }

        try
        {
            var tables = await _meta.GetTablesAsync(model.Connection);

            _saved.Upsert(new SavedConnection
            {
                ServerName = model.Connection.ServerName.Trim(),
                AuthMode = model.Connection.AuthMode,
                UserName = model.Connection.UserName,
                Password = model.Connection.Password
            });

            model.Tables = tables.Select(t => new TableSeedRequest
            {
                SchemaName = t.SchemaName,
                TableName = t.TableName,
                Count = 10,
                Selected = false
            }).ToList();

            ViewBag.Saved = _saved.GetAll();
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Không kết nối được: {ex.Message}");
            ViewBag.Saved = _saved.GetAll();
            return View(model);
        }
    }


    // Step 2 -> modal result (AJAX or fallback full page)
    [HttpPost]
    public async Task<IActionResult> SeedMany(TablesSelectionInput selection)
    {
        try
        {
            var selected = selection.Tables.Where(t => t.Selected).ToList();
            if (!selected.Any())
            {
                ModelState.AddModelError("", "Hãy chọn ít nhất 1 bảng để seed.");
                // For AJAX request return partial with error
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var err = new SeedResult();
                    err.Tables.Add(new PerTableResult
                    {
                        SchemaName = "-",
                        TableName = "-",
                        Inserted = 0,
                        Error = "Hãy chọn ít nhất 1 bảng để seed."
                    });
                    return PartialView("_ResultTable", err);
                }
                ViewBag.Saved = _saved.GetAll();
                return View("Index", selection);
            }

            var result = await _dummy.InsertDummyForTablesAsync(selection.Connection, selected);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ResultTable", result);
            }

            return View("Result", result);
        }
        catch (Exception ex)
        {
            var err = new SeedResult();
            err.Tables.Add(new PerTableResult
            {
                SchemaName = "-",
                TableName = "-",
                Inserted = 0,
                Error = ex.Message
            });

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ResultTable", err);
            }

            return View("Result", err);
        }
    }
}
