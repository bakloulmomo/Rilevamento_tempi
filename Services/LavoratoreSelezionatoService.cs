using System.ComponentModel;
using System.Runtime.ConstrainedExecution;

namespace Rilevamento_tempi.Services;

public sealed class LavoratoreSelezionatoService
{
    public int? IdLavoratore { get; private set; }

    public event Action? LavoratoreCambiato;

    public void Seleziona(int idLavoratore)
    {
        if (idLavoratore <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idLavoratore), "L'ID del lavoratore deve essere positivo.");
        }

        IdLavoratore = idLavoratore;
        LavoratoreCambiato?.Invoke();
    }

    public void Deseleziona()
    {
        IdLavoratore = null;
        LavoratoreCambiato?.Invoke();
    }
}


// La parte del form si trova in Components/Pages/RegistraOperazione.razor, all’inizio: il campo ODP è l’InputNumber associato a codiceOdp. La verifica quando si preme Conferma è nel metodo ConfermaOdpAsync.
// Per impedire davvero di digitare più di 7 cifre, non basta aggiungere maxlength a InputNumber: gli input numerici non applicano quel limite.Il campo andrebbe reso testuale con tastierino numerico e limite di 7 caratteri; conviene poi verificare anche in ConfermaOdpAsync che siano tutte cifre.