using BundIdSpike.ServiceModel;
using ServiceStack;

namespace BundIdSpike.ServiceInterface;

public class HelloService : Service
{
    public object Any(Hello request) => new HelloResponse
    {
        Result = $"Hallo, {(string.IsNullOrEmpty(request.Name) ? "Welt" : request.Name)}! BundID-Spike laeuft.",
    };
}

public class ProfileService : Service
{
    public object Any(SecureProfile request)
    {
        var session = SessionAs<AuthUserSession>()!;
        var meta = session.Meta ?? new Dictionary<string, string>();

        return new SecureProfileResponse
        {
            UserAuthId = session.UserAuthId ?? session.UserAuthName ?? string.Empty,
            DisplayName = session.DisplayName,
            Email = session.Email,
            BundIdSubject = meta.GetValueOrDefault("bundid:sub"),
            Vertrauensniveau = meta.GetValueOrDefault("bundid:vertrauensniveau"),
            Roles = session.Roles ?? new List<string>(),
        };
    }
}
