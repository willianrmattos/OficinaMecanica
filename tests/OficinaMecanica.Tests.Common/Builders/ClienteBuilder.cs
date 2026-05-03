using Bogus;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Tests.Common.Builders;

public class ClienteBuilder
{
    private static readonly Faker Faker = new("pt_BR");

    private string _nome = Faker.Name.FullName();
    private string _documento = "52998224725";
    private string? _email = Faker.Internet.Email();
    private string? _telefone = Faker.Phone.PhoneNumber("###########");

    public ClienteBuilder ComNome(string nome) { _nome = nome; return this; }
    public ClienteBuilder ComDocumento(string documento) { _documento = documento; return this; }
    public ClienteBuilder ComEmail(string? email) { _email = email; return this; }
    public ClienteBuilder SemEmail() { _email = null; return this; }
    public ClienteBuilder SemTelefone() { _telefone = null; return this; }

    public Cliente Build() => new(_nome, Documento.Criar(_documento), _email, _telefone);
}
