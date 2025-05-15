using ApiGateway.WebApp;
using ApiGateway.WebApp.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddServices(builder.Configuration, builder.Environment);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUiWithConsul();
}

app.UseRouting();

app.UseHealthChecks();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAdvancedScopeAuthorization();

app.UseCorrelationId();

app.UseAuthorization();

app.MapControllers();

app.MapReverseProxy();

app.Run();
