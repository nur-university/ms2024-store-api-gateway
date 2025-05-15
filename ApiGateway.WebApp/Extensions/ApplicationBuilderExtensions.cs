using ApiGateway.WebApp.Access;
using Consul;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace ApiGateway.WebApp.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSwaggerWithUi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();           

        return app;
    }

    public static IApplicationBuilder UseSwaggerWithUiWithConsul(this WebApplication app)
    {
        app.UseSwagger();

        var consul = app.Services.GetRequiredService<IConsulClient>();
        var swaggerJsonPath = "/swagger/v1/swagger.json";

        var services = consul.Agent.Services().Result.Response;
        var grouped = services
            .Values
            .GroupBy(s => s.Service) // Agrupa por nombre de servicio
            .Select(g => g.First()); // Usa una instancia por servicio

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/gateway/swagger.json", "API Gateway");

            foreach (var service in grouped)
            {
                var name = service.Service;
                var host = service.Address;
                var port = service.Port;

                var swaggerUrl = $"{host}:{port}{swaggerJsonPath}";
                options.SwaggerEndpoint(swaggerUrl, $"{name} API");
            }
        });

        return app;
    }

    public static IApplicationBuilder UseHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecksUI(options =>
        {
            options.UIPath = "/health-ui";
        });

        return app;
    }

    public static IApplicationBuilder UseAdvancedScopeAuthorization(this WebApplication app)
    {
        app.UseMiddleware<AdvancedScopeAuthorizationMiddleware>();
        return app;
    }

    public static IApplicationBuilder UseCorrelationId(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();
            await next();
        });
        return app;
    }
}
