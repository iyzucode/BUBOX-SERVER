using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Bubox.Infrastructure.Data;

public class DbConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public IDbConnection CreateConnection()
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found in configuration.");

        return new NpgsqlConnection(connectionString);
    }
}
