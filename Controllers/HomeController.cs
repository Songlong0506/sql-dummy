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

    // Step 1 -> Step 2 (list tables) + save/refresh saved connections
    [HttpPost]
    public async Task<IActionResult> Index(ConnectionInput input)
    {
        ViewBag.Saved = _saved.GetAll();
        if (string.IsNullOrWhiteSpace(input.ServerName) || string.IsNullOrWhiteSpace(input.DatabaseName))
        {
            ModelState.AddModelError("", "Vui lòng nhập ServerName và chọn Database Name.");
            return View(new TablesSelectionInput { Connection = input });
        }

        try
        {
            var tables = await _meta.GetTablesAsync(input);

            _saved.Upsert(new SavedConnection
            {
                ServerName = input.ServerName.Trim(),
                AuthMode = input.AuthMode,
                UserName = input.UserName,
                Password = input.Password
            });

            var selection = new TablesSelectionInput
            {
                Connection = input,
                Tables = tables.Select(t => new TableSeedRequest
                {
                    SchemaName = t.SchemaName,
                    TableName = t.TableName,
                    Count = 10,
                    Selected = false
                }).ToList(),
                RulesJson = ""
            };
            return View(selection);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Không kết nối được: {ex.Message}");
            return View(new TablesSelectionInput { Connection = input });
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
                return View("Index", selection);
            }

            RuleConfig? config = null;
            if (!string.IsNullOrWhiteSpace(selection.RulesJson))
            {
                try
                {
                    config = RuleConfig.Parse(selection.RulesJson!);
                }
                catch (Exception exCfg)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        var err = new SeedResult();
                        err.Tables.Add(new PerTableResult
                        {
                            SchemaName = "-",
                            TableName = "-",
                            Inserted = 0,
                            Error = "Rules JSON không hợp lệ: " + exCfg.Message
                        });
                        return PartialView("_ResultTable", err);
                    }
                    ModelState.AddModelError("", $"Rules JSON không hợp lệ: {exCfg.Message}");
                    return View("Index", selection);
                }
            }

            var result = await _dummy.InsertDummyForTablesAsync(selection.Connection, selected, config);

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
