using ApiGateway.WebApp.Access;
using ApiGateway.WebApp.Config;
using ApiGateway.WebApp.Extensions;
using Consul;
using Nur.Store2025.Security;
using Nur.Store2025.Security.Config;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.WebApp;

public static class DependencyInjection
{
    
    public static void AddServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSecrets(configuration, environment)
            .AddSecurityAccessRules(configuration)

            .AddEndpointsApiExplorer()
            .AddSwaggerGen()
            .AddControllers();

        services.AddReverseProxyWithConsul(configuration)
            .AddObservability();
    }

    private static IServiceCollection AddReverseProxyWithConsul(this IServiceCollection services, IConfiguration configuration)
    {
        string configurationName = "ServiceRegistration:ServiceDiscoveryAddress";
        string? serviceDiscoveryAddress = configuration[configurationName];
        services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(cfg =>
        {
            cfg.Address = new Uri(serviceDiscoveryAddress!);
        }));
        services.AddSingleton<IProxyConfigProvider, ConsulYarpConfigProvider>();

        services.AddReverseProxy();
        return services;
    }

    

    

    
}
