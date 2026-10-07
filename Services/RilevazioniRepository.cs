using System.Data;
using Microsoft.Data.SqlClient;

namespace Rilevamento_tempi.Services;

public sealed class RilevazioniRepository
{
    private readonly string _connectionString;

    public RilevazioniRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "DefaultConnection mancante in appsettings.json");
    }

    public async Task<DataTable> ElencaPerLavoratoreAsync(
        int idLavoratore,
        CancellationToken cancellationToken = default)
    {
        var rilevazioni = new DataTable();

        await using var connection = new SqlConnection(_connectionString);
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT
                ID,
                ID_LAVORATORE,
                ODP,
                ID_LAVORAZIONE,
                ORA,
                TIPO_OPERAZIONE
            FROM TABHHTEMP
            WHERE ID_LAVORATORE = @idLavoratore
            ORDER BY ORA DESC";

        command.Parameters.Add("@idLavoratore", SqlDbType.Int).Value = idLavoratore;

        await connection.OpenAsync(cancellationToken);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        rilevazioni.Load(reader);

        return rilevazioni;
    }

    public async Task<string?> UltimaOperazioneAsync(
        int idLavoratore,
        string odp,
        int idLavorazione,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT TOP (1) TIPO_OPERAZIONE
            FROM TABHHTEMP
            WHERE ID_LAVORATORE = @idLavoratore
                AND ODP = @odp
                AND ID_LAVORAZIONE = @idLavorazione
            ORDER BY ORA DESC, ID DESC";

        AggiungiParametriLavorazione(command, idLavoratore, odp, idLavorazione);

        await connection.OpenAsync(cancellationToken);
        var ultimaOperazione = await command.ExecuteScalarAsync(cancellationToken);
        return ultimaOperazione is null or DBNull ? null : Convert.ToString(ultimaOperazione);
    }

    public async Task RegistraAsync(
        int idLavoratore,
        string odp,
        int idLavorazione,
        string tipoOperazione,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)transaction;

        command.CommandText = @"
            SELECT TOP (1) TIPO_OPERAZIONE
            FROM TABHHTEMP WITH (UPDLOCK, HOLDLOCK)
            WHERE ID_LAVORATORE = @idLavoratore
                AND ODP = @odp
                AND ID_LAVORAZIONE = @idLavorazione
            ORDER BY ORA DESC, ID DESC";

        AggiungiParametriLavorazione(command, idLavoratore, odp, idLavorazione);

        var ultimaOperazione = await command.ExecuteScalarAsync(cancellationToken);
        var ultimoTipoOperazione = ultimaOperazione is null or DBNull
            ? null
            : Convert.ToString(ultimaOperazione);

        if (!OperazioneConsentita(ultimoTipoOperazione, tipoOperazione))
        {
            throw new InvalidOperationException(
                $"L'operazione {tipoOperazione} non è consentita dopo {ultimoTipoOperazione ?? "nessuna operazione"} per questa lavorazione.");
        }

        command.CommandText = @"
            INSERT INTO TABHHTEMP
                (ID_LAVORATORE, ODP, ID_LAVORAZIONE, ORA, TIPO_OPERAZIONE)
            VALUES
                (@idLavoratore, @odp, @idLavorazione, @ora, @tipoOperazione)";

        command.Parameters.Clear();
        AggiungiParametriLavorazione(command, idLavoratore, odp, idLavorazione);
        command.Parameters.Add("@ora", SqlDbType.DateTime).Value = DateTime.UtcNow;
        command.Parameters.Add("@tipoOperazione", SqlDbType.NVarChar, 5).Value = tipoOperazione;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static bool OperazioneConsentita(string? ultimaOperazione, string tipoOperazione)
    {
        var operazioneNormalizzata = tipoOperazione.Trim().ToUpperInvariant();
        return ultimaOperazione?.Trim().ToUpperInvariant() switch
        {
            null => operazioneNormalizzata == "START",
            "START" => operazioneNormalizzata is "PAUSA" or "STOP",
            "PAUSA" => operazioneNormalizzata is "START" or "STOP",
            "STOP" => false,
            _ => false
        };
    }

    private static void AggiungiParametriLavorazione(
        SqlCommand command,
        int idLavoratore,
        string odp,
        int idLavorazione)
    {
        command.Parameters.Add("@idLavoratore", SqlDbType.Int).Value = idLavoratore;
        command.Parameters.Add("@odp", SqlDbType.NVarChar, 7).Value = odp;
        command.Parameters.Add("@idLavorazione", SqlDbType.Int).Value = idLavorazione;
    }
}
