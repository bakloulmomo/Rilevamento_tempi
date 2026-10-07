using Microsoft.Data.SqlClient;
using Rilevamento_tempi.Models;

namespace Rilevamento_tempi.Services
{
    public sealed class LavorazioniRepository
    {
        private readonly string _connectionString;

        public LavorazioniRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection mancante in appsettings.json");
        }

        public async Task<List<Lavorazione>> ElencaPerOdpAsync(
            string codiceOdp,
            CancellationToken cancellationToken = default)
        {
            var lavorazioni = new List<Lavorazione>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT dd_riga, dd_note
                FROM HH_CICLI
                WHERE dd_hhodppadre = @odp
                ORDER BY dd_riga";
            command.Parameters.Add("@odp", System.Data.SqlDbType.NVarChar, 7).Value = codiceOdp;

            await connection.OpenAsync(cancellationToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var idOrdinal = reader.GetOrdinal("dd_riga");
            var notaOrdinal = reader.GetOrdinal("dd_note");

            while (await reader.ReadAsync(cancellationToken))
            {
                var id = Convert.ToDecimal(reader.GetValue(idOrdinal));
                if (id <= 0 || id > int.MaxValue || decimal.Truncate(id) != id)
                {
                    throw new InvalidOperationException($"ID lavorazione non valido: dd_riga = {id}. Il valore deve essere un intero positivo.");
                }

                var nota = reader.IsDBNull(notaOrdinal)
                    ? string.Empty
                    : reader.GetValue(notaOrdinal).ToString() ?? string.Empty;
                lavorazioni.Add(new Lavorazione(decimal.ToInt32(id), nota));
            }

            return lavorazioni;
        }
    }
}
