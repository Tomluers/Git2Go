# BundID + ServiceStack + SQL Server - Spike

Technischer Spike: Wie laesst sich **BundID** (id.bund.de, die zentrale
Nutzerkonto-/Login-Plattform des Bundes fuer Online-Verwaltungsdienstleistungen)
als Login-Provider in ein **ServiceStack**-Backend mit **SQL-Server**-Anbindung
integrieren?

Das vollstaendige Ergebnis des Spikes (Vorgehen, Architektur, offene Punkte,
Empfehlung) steht in **[docs/SPIKE.md](docs/SPIKE.md)**. Dieses README
beschreibt nur, wie man den Code startet.

## Projektstruktur

```
src/
  BundIdSpike.ServiceModel/       Request-/Response-DTOs (API-Vertrag)
  BundIdSpike.ServiceInterface/   Service-Implementierungen
  BundIdSpike.Api/                ASP.NET Core Host, AppHost, Auth- und DB-Konfiguration
    Auth/BundIdAuthProvider.cs    Kern des Spikes: OIDC/PKCE-Login gegen BundID
    Configure.Auth.cs             AuthFeature + OrmLite-AuthRepository (SQL Server)
    Configure.Db.cs                OrmLite/SQL-Server-Registrierung
docker-compose.yml                 Lokaler SQL Server fuer die Entwicklung
```

## Voraussetzungen

- .NET 8 SDK
- Docker (fuer einen lokalen SQL Server) oder Zugriff auf eine bestehende
  SQL-Server-Instanz

## Lokal starten

```bash
# 1) Lokalen SQL Server starten
docker compose up -d

# 2) App starten (nutzt appsettings.Development.json)
dotnet run --project src/BundIdSpike.Api
```

Danach:
- `GET /hello/{Name}` - offener Beispiel-Endpunkt (kein Login noetig)
- `GET /secure/profile` - geschuetzter Endpunkt (`[Authenticate]`), liefert
  ohne Login `401 Unauthorized` mit `WWW-Authenticate: bundid ...`
- `GET /auth/bundid` - startet den OIDC-Login-Flow gegen BundID (siehe unten)

Ohne erreichbaren SQL Server startet die App trotzdem (mit einer klaren
Warnung im Log) - nur die Auth-/DB-Endpunkte funktionieren dann nicht.

## BundID-Zugangsdaten konfigurieren

Die Konfiguration liegt in `src/BundIdSpike.Api/appsettings.json` als
flache, gepunktete Schluessel (ServiceStack-Konvention, siehe
`docs/SPIKE.md` fuer die Begruendung):

```json
"oauth.bundid.ConsumerKey": "...",
"oauth.bundid.ConsumerSecret": "...",
"oauth.bundid.AuthorizeUrl": "https://.../idp/authorize",
"oauth.bundid.AccessTokenUrl": "https://.../idp/token",
"oauth.bundid.UserInfoUrl": "https://.../idp/userinfo",
"oauth.bundid.CallbackUrl": "https://localhost:5001/auth/bundid"
```

Reale Werte gibt es erst nach Registrierung des Fachverfahrens bei der
ITZBund (siehe "Offene Punkte" in `docs/SPIKE.md`). Fuer lokale Tests ohne
echte BundID-Zugangsdaten lassen sich Platzhalter-Werte setzen, um den
Redirect-Aufbau (inkl. PKCE `code_challenge`) zu beobachten:

```bash
curl -i http://localhost:5099/auth/bundid
```

liefert dann einen `302`-Redirect mit vollstaendiger Authorize-URL
(inkl. `code_challenge`/`code_challenge_method=S256`).
