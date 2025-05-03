using ApiGateway.WebApp.Access;
using ApiGateway.WebApp.Config;
using Consul;
using Yarp.ReverseProxy.Configuration;
using Nur.Store2025.Security;
using Nur.Store2025.Security.Config;
using Joseco.Secrets.HashicorpVault;
using Joseco.Secrets.Contrats;

namespace ApiGateway.WebApp;

public static class DependencyInjection
{
    private const string JwtOptionsSecretName = "JwtOptions";
    private const string VaultMountPoint = "secrets";

    public static void AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddVault()
            .AddSecurityAccessRules(configuration)

            .AddEndpointsApiExplorer()
            .AddSwaggerGen()
            .AddControllers();

        services.AddReverseProxyWithConsul(configuration)
            .AddAppHealthChecks();
    }

    private static IServiceCollection AddReverseProxyWithConsul(this IServiceCollection services, IConfiguration configuration)
    {
        string configurationName = "ServiceRegistration:ServiceDiscoveryAddress";
        string? serviceDiscoveryAddress = configuration[configurationName];
        services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(cfg =>
        {
            cfg.Address = new Uri("http://localhost:8500");
        }));
        services.AddSingleton<IProxyConfigProvider, ConsulYarpConfigProvider>();

        services.AddReverseProxy();
        return services;
    }

    private static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        //Add health check for this web application
        services.AddHealthChecks();

        services.AddHealthChecksUI(setupSettings: setup =>
        {
            setup.SetEvaluationTimeInSeconds(10); // cada 10 seg
            setup.MaximumHistoryEntriesPerEndpoint(50);

            var registerdServices = services.BuildServiceProvider()
                .GetServices<IConsulClient>()
                .SelectMany(client => client.Agent.Services().Result.Response.Values)
                .Select(service => service)
                .ToList();


            foreach (var service in registerdServices)
            {
                setup.AddHealthCheckEndpoint(service.Service, $"{service.Address}:{service.Port}/health");
            }
        })
        .AddInMemoryStorage();

        return services;
    }

    private static IServiceCollection AddSecurityAccessRules(this IServiceCollection services, IConfiguration configuration)
    {
        JwtOptions jwtOptions = services.BuildServiceProvider()
            .GetRequiredService<JwtOptions>();
        services.AddSecurity(jwtOptions, Nur.Store2025.Access.Permission.PermissionsList);
        services.AddSingleton(new ScopeRouteMatcher(Nur.Store2025.Access.Routes.RouteList));
        return services;
    }

    private static IServiceCollection AddVault(this IServiceCollection services)
    {
        string? vaultUrl = Environment.GetEnvironmentVariable("VAULT_URL");
        string? vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");

        if (string.IsNullOrEmpty(vaultUrl) || string.IsNullOrEmpty(vaultToken))
        {
            throw new InvalidOperationException("Vault URL or Token is not set in environment variables.");
        }

        var settings = new VaultSettings()
        {
            VaultUrl = vaultUrl,
            VaultToken = vaultToken
        };

        services.AddHashicorpVault(settings)
            .LoadSecretsFromVault();

        return services;
    }
    private static void LoadSecretsFromVault(this IServiceCollection services)
    {
        using var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        using var scope = scopeFactory.CreateScope();
        var secretManager = scope.ServiceProvider.GetRequiredService<ISecretManager>();

        Task[] tasks = [
                LoadAndRegister<JwtOptions>(secretManager, services, JwtOptionsSecretName, VaultMountPoint),
            ];

        Task.WaitAll(tasks);
    }

    private static async Task LoadAndRegister<T>(ISecretManager secretManager, IServiceCollection services,
        string secretName, string mountPoint) where T : class, new()
    {
        T secret = await secretManager.Get<T>(secretName, mountPoint);
        services.AddSingleton<T>(secret);
    }
}
