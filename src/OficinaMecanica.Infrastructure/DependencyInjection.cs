using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Repositories;
using OficinaMecanica.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace OficinaMecanica.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b
                    .MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
                    // Azure SQL Database serverless pausa por inatividade (auto_pause_delay_in_minutes,
                    // ver OficinaMecanica.Infra) - a primeira conexao apos o pause falha com erro
                    // transitorio 40613 ("nao esta disponivel, tente novamente") enquanto o banco acorda.
                    // Sem isso, essa falha subia como 500 pro cliente em vez de so esperar e tentar de novo.
                    .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)
            ));

        // Unit of Work
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());

        // Repositories
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IVeiculoRepository, VeiculoRepository>();
        services.AddScoped<IServicoRepository, ServicoRepository>();
        services.AddScoped<IPecaRepository, PecaRepository>();
        services.AddScoped<IOrdemDeServicoRepository, OrdemDeServicoRepository>();

        // Email Service
        services.AddScoped<IEmailService, SmtpEmailService>();

        // JWT Authentication - so valida tokens RS256 emitidos pelo OficinaMecanica.Seguranca,
        // buscando a chave publica dinamicamente via JWKS (nao emite mais token aqui)
        var jwtSettings = configuration.GetSection("JwtSettings");
        var jwksUri = jwtSettings["JwksUri"]!;
        // HttpDocumentRetriever recusa URL http:// por padrao (RequireHttps = true) - em
        // producao o JwksUri sempre passa pela APIM (https), mas em dev local o Seguranca
        // roda sem TLS (http://host.docker.internal:7071/...), entao precisa liberar so
        // nesse caso.
        var documentRetriever = new HttpDocumentRetriever
        {
            RequireHttps = jwksUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        };
        var configManager = new ConfigurationManager<JsonWebKeySet>(jwksUri, new JsonWebKeySetRetriever(), documentRetriever);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                    configManager.GetConfigurationAsync().GetAwaiter().GetResult().Keys.Where(k => k.Kid == kid),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        return services;
    }
}
