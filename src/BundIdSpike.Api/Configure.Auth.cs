using BundIdSpike.Api.Auth;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Data;

[assembly: HostingStartup(typeof(BundIdSpike.Api.ConfigureAuth))]

namespace BundIdSpike.Api;

/// <summary>
/// Registriert BundID als Login-Provider sowie die Persistierung von Nutzer- und
/// Session-Daten ueber den OrmLite-AuthRepository (SQL Server, siehe Configure.Db.cs).
/// </summary>
public class ConfigureAuth : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context, services) =>
        {
            var appSettings = new NetCoreAppSettings(context.Configuration);

            // InitSchema() wird bewusst NICHT hier (lazy, beim ersten DI-Resolve) aufgerufen,
            // sondern explizit beim App-Start in AppHost.Configure() - siehe AppHost.cs.
            // So schlaegt eine nicht erreichbare SQL-Server-Instanz sofort beim Start fehl
            // (mit klarer Fehlermeldung), statt den ersten Login-Request lange haengen zu
            // lassen (SqlClient-Verbindungsaufbau/-Timeout).
            services.AddSingleton<IAuthRepository>(c =>
                new OrmLiteAuthRepository<UserAuth, UserAuthDetails>(
                    c.GetRequiredService<IDbConnectionFactory>()));

            services.AddPlugin(new AuthFeature(
                () => new AuthUserSession(),
                new IAuthProvider[]
                {
                    new BundIdAuthProvider(appSettings),
                }));
        });
}
