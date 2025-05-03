using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;


namespace ApiGateway.WebApp.Config;

public class ConsulYarpConfigProvider : IProxyConfigProvider
{
    private readonly ILogger<ConsulYarpConfigProvider> _logger;
    private readonly IConsulClient _consulClient;
    private readonly Timer _timer;
    private volatile ConsulProxyConfig _config;

    public ConsulYarpConfigProvider(ILogger<ConsulYarpConfigProvider> logger, IConsulClient consulClient)
    {
        _logger = logger;
        _consulClient = consulClient;
        _config = new ConsulProxyConfig([], []);

        _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(30)); // refresca cada 30s
        
    }

    public IProxyConfig GetConfig() => _config;

    private async void Refresh(object state)
    {
        try
        {
            var services = await _consulClient.Agent.Services();
            var clusters = new Dictionary<string, ClusterConfig>();
            var routes = new Dictionary<string, Yarp.ReverseProxy.Configuration.RouteConfig>();

            foreach (var serviceEntry in services.Response.Values)
            {
                var serviceName = serviceEntry.Service;


                var serviceId = serviceEntry.ID;
                var clusterId = $"{serviceName}-cluster";
                var routePrefix = $"/api/{serviceName}";

                var destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
                {
                    [serviceId] = new Yarp.ReverseProxy.Configuration.DestinationConfig
                    {
                        Address = $"{serviceEntry.Address}:{serviceEntry.Port}"
                    }
                };

                clusters.Add(clusterId, new ClusterConfig
                {
                    ClusterId = clusterId,
                    Destinations = destinations
                });

                routes.Add(clusterId, new Yarp.ReverseProxy.Configuration.RouteConfig
                {
                    RouteId = $"{serviceName}-route",
                    ClusterId = clusterId,
                    Match = new RouteMatch
                    {
                        Path = $"{routePrefix}/{{**catch-all}}"
                    },
                    Transforms = new List<Dictionary<string, string>>
                    {
                        new() { { "PathRemovePrefix", routePrefix } },
                        new() { { "PathPrefix", "/api" } }
                    }
                });
            }

            _config = new ConsulProxyConfig([.. routes.Values], [.. clusters.Values]);
            _logger.LogInformation("Configuración de YARP actualizada desde Consul con {RouteCount} rutas y {ClusterCount} clusters", routes.Count, clusters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando configuración de YARP desde Consul");
        }
    }

    private class ConsulProxyConfig(IReadOnlyList<Yarp.ReverseProxy.Configuration.RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) : Yarp.ReverseProxy.Configuration.IProxyConfig
    {
        public IReadOnlyList<Yarp.ReverseProxy.Configuration.RouteConfig> Routes { get; } = routes;
        public IReadOnlyList<ClusterConfig> Clusters { get; } = clusters;
        public IChangeToken ChangeToken { get; } = new CancellationChangeToken(new CancellationTokenSource().Token); // no hot reload
    }
}