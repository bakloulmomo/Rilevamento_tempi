# Rilevamento tempi

Web app per la **rilevazione dei tempi di lavorazione** in produzione.
L'operatore si identifica, inserisce il codice dell'ordine di produzione (ODP), sceglie la fase di lavorazione e registra **Start / Pausa / Stop**. Ogni evento viene salvato a database, così da conoscere il tempo reale speso su ogni fase di ogni ODP e da chi.

Pensata per l'uso a bordo macchina da tablet o PC in rete aziendale.

---

## Indice

- [Funzionalità](#funzionalità)
- [Tecnologie](#tecnologie)
- [Requisiti](#requisiti)
- [Database](#database)
- [Configurazione](#configurazione)
- [Avvio in sviluppo](#avvio-in-sviluppo)
- [Pubblicazione](#pubblicazione)
- [Struttura del progetto](#struttura-del-progetto)
- [Limiti noti](#limiti-noti)

---

## Funzionalità

| Pagina | Percorso | Descrizione |
|---|---|---|
| Seleziona lavoratore | `/lavoratori` | Elenco degli operatori: l'utente sceglie il proprio nome. La scelta resta valida per la sessione. |
| Lavori | `/` | Inserimento codice ODP (1–7 cifre), scelta della lavorazione e pulsanti Start / Pausa / Stop. |
| Tempi fatti | `/tempi` | Storico delle registrazioni dell'operatore, dalla più recente. |

### Flusso
1. L'operatore seleziona il proprio nome.
2. Inserisce il codice ODP e preme **Conferma**: vengono caricate le lavorazioni (fasi del ciclo) di quell'ODP.
3. Seleziona una lavorazione: l'app legge l'ultimo stato registrato e abilita solo i pulsanti consentiti.
4. Preme **Start**, **Pausa** o **Stop**: l'evento viene salvato con data e ora.

### Sequenza delle operazioni

Per ogni combinazione *operatore + ODP + lavorazione*:

| Ultima operazione | Operazioni consentite |
|---|---|
| nessuna | Start |
| Start | Pausa, Stop |
| Pausa | Start, Stop |
| Stop | nessuna (lavorazione chiusa) |

Il controllo è ripetuto lato server dentro una transazione `SERIALIZABLE`, così due richieste contemporanee non possono produrre sequenze non valide.

---

## Tecnologie

- [.NET 10](https://dotnet.microsoft.com/) – Blazor Web App, render mode **Interactive Server**
- SQL Server tramite `Microsoft.Data.SqlClient`
- Bootstrap 5 per l'interfaccia

---

## Requisiti

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Un'istanza SQL Server raggiungibile, con le tabelle descritte sotto
- (Facoltativo) Visual Studio 2022 o VS Code con C# Dev Kit

---

## Database

L'app legge due tabelle del gestionale esistente e scrive in una tabella propria.

### Tabelle lette

| Tabella | Colonne usate | Uso |
|---|---|---|
| `tabcope` | `tb_codcope` (codice operatore), `tb_descope` (nome) | Elenco operatori. Il codice deve essere un intero positivo. |
| `HH_CICLI` | `dd_hhodppadre` (ODP), `dd_riga` (id lavorazione), `dd_note` (descrizione) | Lavorazioni di un ODP. |

### Tabella scritta: `TABHHTEMP`

Struttura minima attesa dal codice (adattare tipi e indici alle proprie esigenze):

```sql
CREATE TABLE dbo.TABHHTEMP (
    ID               INT IDENTITY(1,1) PRIMARY KEY,
    ID_LAVORATORE    INT          NOT NULL,
    ODP              NVARCHAR(7)  NOT NULL,
    ID_LAVORAZIONE   INT          NOT NULL,
    ORA              DATETIME     NOT NULL,  -- salvata in UTC
    TIPO_OPERAZIONE  NVARCHAR(5)  NOT NULL   -- 'Start' | 'Pausa' | 'Stop'
);

CREATE INDEX IX_TABHHTEMP_Lavorazione
    ON dbo.TABHHTEMP (ID_LAVORATORE, ODP, ID_LAVORAZIONE, ORA DESC, ID DESC);
```

### Permessi consigliati

Usare un utente dedicato con i soli permessi necessari (mai `sa` o `db_owner`):

```sql
GRANT SELECT          ON dbo.tabcope   TO [utente_rilevamento];
GRANT SELECT          ON dbo.HH_CICLI  TO [utente_rilevamento];
GRANT SELECT, INSERT  ON dbo.TABHHTEMP TO [utente_rilevamento];
```

---

## Configurazione

L'app richiede una connection string chiamata **`DefaultConnection`**.

> ⚠️ **Non inserire mai la connection string in `appsettings.json` o in altri file versionati.**

### In sviluppo – User Secrets

Dalla cartella del progetto:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=SERVER;Database=DATABASE;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
```

I segreti vengono salvati fuori dal repository (in `%APPDATA%\Microsoft\UserSecrets\` su Windows, `~/.microsoft/usersecrets/` su macOS/Linux).

### In produzione – variabile d'ambiente

```bash
ConnectionStrings__DefaultConnection="Server=SERVER;Database=DATABASE;Integrated Security=True;Encrypt=True"
```

Dove possibile preferire l'**autenticazione Windows** (account di servizio / gMSA) a utente e password SQL.

---

## Avvio in sviluppo

```bash
git clone https://github.com/bakloulmomo/Rilevamento_tempi.git
cd Rilevamento_tempi
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection string>"
dotnet run --launch-profile https
```

L'app è disponibile su:
- https://localhost:7164
- http://localhost:5126

---

## Pubblicazione

```bash
dotnet publish -c Release -o ./publish
```

La cartella `publish` può essere ospitata su IIS (con l'ASP.NET Core Hosting Bundle per .NET 10) oppure eseguita come servizio. Impostare `ConnectionStrings__DefaultConnection` come variabile d'ambiente del server o del pool applicativo.

---

## Struttura del progetto

```
Rilevamento_tempi/
├── Program.cs                          # Avvio app e registrazione servizi
├── Models/
│   └── Lavorazione.cs                  # Record Id + Nota della lavorazione
├── Services/
│   ├── LavoratoreSelezionatoService.cs # Operatore selezionato nella sessione
│   ├── LavoratoriRepository.cs         # Lettura operatori (tabcope)
│   ├── LavorazioniRepository.cs        # Lettura lavorazioni per ODP (HH_CICLI)
│   └── RilevazioniRepository.cs        # Lettura/scrittura tempi (TABHHTEMP)
├── Components/
│   ├── Pages/
│   │   ├── SelezionaLavoratore.razor   # /lavoratori
│   │   ├── RegistraOperazione.razor    # /
│   │   └── TempiRegistrati.razor       # /tempi
│   └── Layout/                         # Layout, menu, modale di riconnessione
└── wwwroot/                            # CSS, favicon, Bootstrap
```
