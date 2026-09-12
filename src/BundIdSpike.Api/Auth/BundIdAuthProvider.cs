using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Configuration;
using ServiceStack.Text;

namespace BundIdSpike.Api.Auth;

/// <summary>
/// OIDC Authorization-Code-Flow gegen die BundID (id.bund.de) fuer ServiceStack.
///
/// Baut auf ServiceStack's generischem <see cref="OAuth2Provider"/> auf (dieselbe Basis,
/// die auch die eingebauten Provider fuer Github/Microsoft/etc. verwenden) und ergaenzt:
///  - Laden der Nutzerinformationen vom BundID-UserInfo-Endpoint (<see cref="UserInfoUrl"/>)
///  - Mapping der BundID-/FIM-spezifischen Claims (pseudonyme Kennung, Vertrauensniveau)
///  - PKCE (RFC 7636, S256) ueber die von <see cref="ServiceStack.Auth.AuthProvider"/>
///    bereitgestellten Url-Filter-Hooks
///
/// Endpunkte, Client-Credentials und Scopes werden ueber die ServiceStack-AppSettings-
/// Konvention "oauth.bundid.*" konfiguriert (appsettings.json Abschnitt "oauth:bundid",
/// siehe appsettings.json in diesem Projekt).
///
/// WICHTIGER HINWEIS (Spike-Ergebnis): Die konkreten Endpoint-URLs, Client-Credentials,
/// Scopes und vor allem die exakten Claim-Namen (insbesondere fuer das "Vertrauensniveau"/
/// Level-of-Assurance und die eID-Attribute) sind ohne Registrierung bei der ITZBund und
/// Zugang zur BundID-Testumgebung nicht oeffentlich verifizierbar. Die hier hinterlegten
/// Claim-Namen sind sinnvolle Defaults nach OIDC-Standard und muessen gegen die technische
/// Dokumentation der BundID abgeglichen werden, siehe docs/SPIKE.md ("Offene Punkte").
/// </summary>
public class BundIdAuthProvider : OAuth2Provider
{
    public const string Name = "bundid";
    public const string Realm = "https://id.bund.de/";

    /// <summary>UserInfo-Endpoint der BundID (aus dem OIDC-Discovery-Dokument der jeweiligen Umgebung).</summary>
    public string? UserInfoUrl { get; set; }

    // Claim-Namen gemaess OIDC-Standard bzw. (bestmoeglich recherchierter) BundID/FIM-Spezifikation.
    // TODO(Spike): gegen die technische Dokumentation/Testumgebung der BundID verifizieren.
    public string SubjectClaim { get; set; } = "sub";
    public string DisplayNameClaim { get; set; } = "name";
    public string EmailClaim { get; set; } = "email";
    public string TrustLevelClaim { get; set; } = "urn:zwd:1.0:loa";

    public BundIdAuthProvider(IAppSettings appSettings) : base(appSettings, Realm, Name)
    {
        UserInfoUrl = appSettings.GetString($"oauth.{Provider}.{nameof(UserInfoUrl)}");

        // --- PKCE (RFC 7636, code_challenge_method=S256) ---
        // ServiceStack's OAuth2Provider verwendet die ServiceStack-Session-Id 1:1 als
        // OAuth "state"-Parameter (siehe ServiceStack.Auth.OAuth2Provider.AuthenticateAsync)
        // und persistiert die Session bereits VOR dem Redirect zum Authorize-Endpoint.
        // Der PKCE-"code_verifier" wird deshalb bewusst NICHT in der Session, sondern
        // ueber den Server-Cache (HostContext.Cache) korreliert ueber genau diese Id
        // zwischengespeichert - das funktioniert unabhaengig vom Session-Speicherzeitpunkt.
        PreAuthUrlFilter = (ctx, url) =>
        {
            var verifier = GenerateCodeVerifier();
            var challenge = ToCodeChallenge(verifier);
            HostContext.Cache.Set(PkceCacheKey(ctx.Session.Id), verifier, TimeSpan.FromMinutes(10));

            return url
                .AddQueryParam("code_challenge", challenge)
                .AddQueryParam("code_challenge_method", "S256");
        };

        AccessTokenUrlFilter = (ctx, url) =>
        {
            var cacheKey = PkceCacheKey(ctx.Session.Id);
            var verifier = HostContext.Cache.Get<string>(cacheKey);
            if (string.IsNullOrEmpty(verifier))
                return url; // kein PKCE-Verifier vorhanden -> Request unveraendert durchreichen

            HostContext.Cache.Remove(cacheKey);
            return url.AddQueryParam("code_verifier", verifier);
        };
    }

    static string PkceCacheKey(string sessionId) => $"bundid:pkce:{sessionId}";

    static string GenerateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    static string ToCodeChallenge(string codeVerifier) =>
        Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Ruft den UserInfo-Endpoint der BundID mit dem erhaltenen Access-Token auf.</summary>
    protected override async Task<Dictionary<string, string>> CreateAuthInfoAsync(
        string accessToken, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(UserInfoUrl))
            throw new NotSupportedException($"oauth.{Provider}.{nameof(UserInfoUrl)} ist nicht konfiguriert.");

        var json = await UserInfoUrl.GetJsonFromUrlAsync(
            requestFilter: req => req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken),
            token: token).ConfigAwait();

        using var doc = JsonDocument.Parse(json);
        var claims = new Dictionary<string, string>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            claims[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString() ?? string.Empty
                : prop.Value.GetRawText();
        }
        return claims;
    }

    /// <summary>Mappt die BundID-Claims auf die ServiceStack-Session/UserAuth-Datensaetze.</summary>
    protected override async Task LoadUserAuthInfoAsync(AuthUserSession userSession, IAuthTokens tokens,
        Dictionary<string, string> authInfo, CancellationToken token = default)
    {
        authInfo.TryGetValue(SubjectClaim, out var subject);
        if (!string.IsNullOrEmpty(subject))
        {
            tokens.UserId = subject;
            tokens.UserName ??= subject;
        }
        if (authInfo.TryGetValue(DisplayNameClaim, out var name))
            tokens.DisplayName = name;
        if (authInfo.TryGetValue(EmailClaim, out var email))
            tokens.Email = email;

        await base.LoadUserAuthInfoAsync(userSession, tokens, authInfo, token).ConfigAwait();

        userSession.UserAuthName ??= tokens.UserId;
        userSession.Meta ??= new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(subject))
            userSession.Meta["bundid:sub"] = subject;
        if (authInfo.TryGetValue(TrustLevelClaim, out var trustLevel))
            userSession.Meta["bundid:vertrauensniveau"] = trustLevel;
    }
}
