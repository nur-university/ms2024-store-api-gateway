using Consul;

namespace ApiGateway.WebApp.Extensions;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services)
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
}
