using ServiceStack;
using ServiceStack.OrmLite;

[assembly: HostingStartup(typeof(BundIdSpike.Api.ConfigureDb))]

namespace BundIdSpike.Api;

/// <summary>
/// Registriert die SQL-Server-Anbindung ueber ServiceStack.OrmLite. Die Verbindung
/// wird sowohl fuer die eigenen Services (base.Db) als auch fuer den OrmLite-basierten
/// AuthRepository (siehe Configure.Auth.cs) verwendet.
/// </summary>
public class ConfigureDb : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context, services) =>
        {
            var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection fehlt in der Konfiguration (appsettings.json).");

            services.AddOrmLite(options => options.UseSqlServer(connectionString, dialect => { }));
        });
}
