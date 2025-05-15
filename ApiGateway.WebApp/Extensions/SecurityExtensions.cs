using ApiGateway.WebApp.Access;
using Nur.Store2025.Security;
using Nur.Store2025.Security.Config;

namespace ApiGateway.WebApp.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddSecurityAccessRules(this IServiceCollection services, IConfiguration configuration)
    {
        JwtOptions jwtOptions = services.BuildServiceProvider()
            .GetRequiredService<JwtOptions>();
        services.AddSecurity(jwtOptions, Nur.Store2025.Access.Permission.PermissionsList);
        services.AddSingleton(new ScopeRouteMatcher(Nur.Store2025.Access.Routes.RouteList));
        return services;
    }
}
