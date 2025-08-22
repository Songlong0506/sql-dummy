using Microsoft.Data.SqlClient;
using SqlDummySeeder.Models;

namespace SqlDummySeeder.Services;

public class SqlConnectionFactory
{
    public SqlConnection Create(ConnectionInput input)
    {
        var sb = new SqlConnectionStringBuilder
        {
            DataSource = input.ServerName,
            InitialCatalog = input.DatabaseName,
            TrustServerCertificate = true,
            ConnectTimeout = 10
        };

        if (string.Equals(input.AuthMode, "Sql", StringComparison.OrdinalIgnoreCase))
        {
            sb.IntegratedSecurity = false;
            sb.UserID = input.UserName;
            sb.Password = input.Password;
        }
        else
        {
            sb.IntegratedSecurity = true;
        }

        return new SqlConnection(sb.ConnectionString);
    }

    public SqlConnection CreateForServer(ConnectionInput input)
    {
        var sb = new SqlConnectionStringBuilder
        {
            DataSource = input.ServerName,
            InitialCatalog = "master",
            TrustServerCertificate = true,
            ConnectTimeout = 10
        };

        if (string.Equals(input.AuthMode, "Sql", StringComparison.OrdinalIgnoreCase))
        {
            sb.IntegratedSecurity = false;
            sb.UserID = input.UserName;
            sb.Password = input.Password;
        }
        else
        {
            sb.IntegratedSecurity = true;
        }

        return new SqlConnection(sb.ConnectionString);
    }
}
