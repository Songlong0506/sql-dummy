using System.Text.Json;

namespace SqlDummySeeder.Services;

public class SavedConnection
{
    public string ServerName { get; set; } = "";
    public string AuthMode { get; set; } = "Windows"; // "Windows" or "Sql"
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

public class SavedConnectionsService
{
    private readonly string _storePath;
    private readonly object _gate = new();

    public SavedConnectionsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SqlDummySeeder"
        );
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "saved-connections.json");
    }

    public List<SavedConnection> GetAll()
    {
        try
        {
            if (!File.Exists(_storePath)) return new();
            var json = File.ReadAllText(_storePath);
            var list = JsonSerializer.Deserialize<List<SavedConnection>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return list ?? new();
        }
        catch
        {
            return new();
        }
    }

    public void Upsert(SavedConnection item)
    {
        lock (_gate)
        {
            var list = GetAll();
            var existing = list.FirstOrDefault(x => string.Equals(x.ServerName, item.ServerName, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                list.Add(item);
            }
            else
            {
                existing.AuthMode = item.AuthMode;
                existing.UserName = item.UserName;
                existing.Password = item.Password;
            }

            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storePath, json);
        }
    }

    public void Delete(string serverName)
    {
        lock (_gate)
        {
            var list = GetAll();
            list = list.Where(x => !string.Equals(x.ServerName, serverName, StringComparison.OrdinalIgnoreCase)).ToList();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storePath, json);
        }
    }
}
