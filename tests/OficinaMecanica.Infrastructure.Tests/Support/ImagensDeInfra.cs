namespace OficinaMecanica.Infrastructure.Tests.Support;

// Imagens dos testes de infraestrutura. Fixo as duas pelo tag de patch (CU27 no SQL Server, 4.4.0 no RabbitMQ): o tag
// flutuante 2022-latest anda, e o digest de um tag que andou pode deixar de ser servido pelo registro.
public static class ImagensDeInfra
{
    public const string SqlServer = "mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04";

    public const string RabbitMq = "rabbitmq:4.4.0-management";
}
