using ServiceStack;

namespace BundIdSpike.ServiceModel;

/// <summary>
/// Geschuetzter Endpunkt: liefert die ueber BundID authentifizierten Nutzerdaten
/// aus der ServiceStack-Session (persistiert via SQL Server/OrmLite).
/// </summary>
[Route("/secure/profile", "GET")]
[Authenticate]
public class SecureProfile : IReturn<SecureProfileResponse>
{
}

public class SecureProfileResponse
{
    public string UserAuthId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }

    /// <summary>Pseudonyme, dienstespezifische BundID-Kennung (OIDC "sub"-Claim).</summary>
    public string? BundIdSubject { get; set; }

    /// <summary>Vertrauensniveau der BundID-Anmeldung (z.B. niedrig/substanziell/hoch).</summary>
    public string? Vertrauensniveau { get; set; }

    public List<string> Roles { get; set; } = new();
}
