using System.Data;
using Microsoft.Data.SqlClient;

namespace Rilevamento_tempi.Services;

public sealed class LavoratoriRepository
{
    private readonly string _connectionString;

    public LavoratoriRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection mancante in appsettings.json");
    }

    public async Task<DataTable> ElencaAsync(CancellationToken cancellationToken = default)
    {
        var lavoratori = new DataTable();
        await using var connection = new SqlConnection(_connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT tb_codcope, tb_descope FROM tabcope ORDER BY tb_descope";

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        lavoratori.Load(reader);
        return lavoratori;
    }
}
