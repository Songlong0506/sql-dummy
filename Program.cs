using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlDummySeeder.Services;
using SqlDummySeeder.Excel.Data;
using SqlDummySeeder.Excel.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddSingleton<SavedConnectionsService>();
builder.Services.AddScoped<SqlMetadataService>();
builder.Services.AddScoped<DummyDataService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=excelDummy.db"));
builder.Services.AddScoped<IExcelGenerator, ExcelGenerator>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
