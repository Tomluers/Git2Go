using ServiceStack;
using ServiceStack.Auth;

[assembly: HostingStartup(typeof(BundIdSpike.Api.AppHost))]

namespace BundIdSpike.Api;

public class AppHost() : AppHostBase("BundID ServiceStack Spike"), IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services =>
        {
            // Weitere ASP.NET Core IOC Dependencies bei Bedarf hier registrieren.
        });

    public override void Configure()
    {
        SetConfig(new HostConfig
        {
            // Fuer den Spike bewusst offen (keine Produktions-Haertung, z.B. CORS/HTTPS-Only).
        });

        // Schema (UserAuth/UserAuthDetails-Tabellen) explizit beim Start anlegen, statt
        // beim ersten Login-Request (siehe Configure.Auth.cs). Ein nicht erreichbarer
        // SQL Server fuehrt so zu einer klaren Warnung beim App-Start; Endpunkte ohne
        // DB-Zugriff (z.B. /hello) bleiben trotzdem nutzbar, um den Spike lokal auch ohne
        // SQL Server explorieren zu koennen.
        try
        {
            Resolve<IAuthRepository>().InitSchema();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[BundID Spike] Konnte AuthRepository-Schema nicht initialisieren " +
                $"(SQL Server erreichbar? ConnectionStrings:DefaultConnection korrekt?): {ex.Message}");
        }
    }
}
