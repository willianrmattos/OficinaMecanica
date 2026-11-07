using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// IHostEnvironment para os testes que montam so um ServiceCollection, sem Host: o validador das opcoes da
// mensageria depende do nome do ambiente.
public sealed class AmbienteFalso : IHostEnvironment
{
    public AmbienteFalso(string nome) => EnvironmentName = nome;

    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "OficinaMecanica.Infrastructure.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
