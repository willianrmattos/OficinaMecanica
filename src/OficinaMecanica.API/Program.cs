using OficinaMecanica.Application;
using OficinaMecanica.Infrastructure;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.API.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var otelEndpoint = builder.Configuration["Otel:Endpoint"];
if (!string.IsNullOrWhiteSpace(otelEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("OficinaMecanica.API"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddSqlClientInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint)));
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OficinaMecanica API",
        Version = "v1",
        Description = "API do Sistema de Gestão para Oficina Mecânica"
    });

    // IDs de operação únicos por controller+action - vários métodos (ObterPorId,
    // Criar, Listar, Atualizar) se repetem entre controllers. Sem isso, o import
    // do OpenAPI no Azure API Management gera nomes de operação genéricos no
    // primeiro import e fica instável em reimports futuros (a APIM casa por
    // operationId pra saber o que atualizar vs. apagar).
    c.CustomOperationIds(apiDesc =>
        $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.ActionDescriptor.RouteValues["action"]}");

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando o esquema Bearer. Exemplo: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Scheme/Host (X-Forwarded-Proto/-Host) nao sao lidos automaticamente pelo
// ASP.NET Core so por chegar o header - precisa desse middleware pra
// confiar neles. Sem isso, HttpRequest.Scheme/Host continuam refletindo o
// que o pod realmente recebe (http, IP interno do ingress-nginx), nao o
// que o cliente usou pra chegar na APIM (https, apimfiap.azure-api.net).
// KnownNetworks/KnownProxies limpos porque o IP de quem repassa (pod do
// ingress-nginx) nao e fixo/conhecido de antemao.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Le o prefixo que a APIM manda (header X-Forwarded-Prefix, setado via
// policy em OficinaMecanica.Infra/apim/) e ajusta o PathBase da request - sem isso, os
// links absolutos que o Swagger saem sem o prefixo.
app.Use((context, next) =>
{
    var prefix = context.Request.Headers["X-Forwarded-Prefix"].FirstOrDefault();
    if (!string.IsNullOrEmpty(prefix))
        context.Request.PathBase = new PathString(prefix);
    return next();
});

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpMetrics();

// Auto-migrate in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

app.UseSwagger(c =>
{
    // servers[] calculado por requisicao (nao fixo) - reflete o PathBase
    // acima, entao o spec e o "Try it out" apontam pro prefixo certo tanto
    // direto (sem prefixo) quanto atras da APIM (/oficinaserver).
    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
    {
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new() { Url = $"{httpReq.Scheme}://{httpReq.Host}{httpReq.PathBase}" }
        };
    });
});
app.UseSwaggerUI(c =>
{
    // Relativo (sem "/" no inicio) - resolve certo tanto acessando direto
    // quanto com o PathBase da APIM na frente, sem precisar saber o
    // prefixo de antemao.
    c.SwaggerEndpoint("swagger/v1/swagger.json", "OficinaMecanica API v1");
    c.RoutePrefix = string.Empty;
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapMetrics();
app.MapHealthChecks("/health");

Log.Information("OficinaMecanica API iniciada com sucesso.");

app.Run();

// Required for integration tests
public partial class Program { }
