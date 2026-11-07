namespace OficinaMecanica.Infrastructure.Tests.Support;

// Imagens dos testes de infraestrutura. O SQL Server segue um tag flutuante (2022-latest), entao o digest e o que
// de fato o fixa; o RabbitMQ ja e fixado pelo tag de patch.
public static class ImagensDeInfra
{
    public const string SqlServer =
        "mcr.microsoft.com/mssql/server:2022-latest@sha256:d38c2a64812d775f844088e4e44ab33846eefc61157431bf9c8a3943a534c22e";

    public const string RabbitMq = "rabbitmq:4.4.0-management";
}
