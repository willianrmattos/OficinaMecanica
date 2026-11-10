using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OficinaMecanica.Infrastructure.Messaging;

// Transporte em memoria e persistencia "Nenhuma" existem so para os testes. Sem esta guarda, uma variavel de
// ambiente esquecida (Mensageria__Transporte=EmMemoria) subiria a API no AKS sem broker e sem outbox, perdendo
// mensagem em silencio. Para valor fora do enum (o binder aceita "0" ou "9"), a primeira barreira e o AddMensageria,
// cujos switch lancam; este validador e a segunda, em defesa em profundidade, e olha o valor resultante da
// configuracao, nao o texto.
public sealed class ValidadorDasOpcoesDaMensageria : IValidateOptions<OpcoesDaMensageria>
{
    private readonly IHostEnvironment _ambiente;

    public ValidadorDasOpcoesDaMensageria(IHostEnvironment ambiente)
    {
        _ambiente = ambiente;
    }

    public ValidateOptionsResult Validate(string? name, OpcoesDaMensageria opcoes)
    {
        var falhas = new List<string>();
        var ambienteDeTeste = _ambiente.IsDevelopment() || _ambiente.IsEnvironment("Testing");

        if (!Enum.IsDefined(opcoes.Transporte))
            falhas.Add(OpcoesDaMensageria.MensagemDeValorInvalido(nameof(opcoes.Transporte), (int)opcoes.Transporte, OpcoesDaMensageria.TransportesAceitos));
        else if (!ambienteDeTeste && opcoes.Transporte != TransporteDaMensageria.RabbitMq)
            falhas.Add($"Mensageria:Transporte deve ser RabbitMq no ambiente '{_ambiente.EnvironmentName}': EmMemoria só é permitido em Development e Testing, porque sobe a API sem broker.");

        if (!Enum.IsDefined(opcoes.Persistencia))
            falhas.Add(OpcoesDaMensageria.MensagemDeValorInvalido(nameof(opcoes.Persistencia), (int)opcoes.Persistencia, OpcoesDaMensageria.PersistenciasAceitas));
        else if (!ambienteDeTeste && opcoes.Persistencia != PersistenciaDaMensageria.EntityFramework)
            falhas.Add($"Mensageria:Persistencia deve ser EntityFramework no ambiente '{_ambiente.EnvironmentName}': Nenhuma só é permitido em Development e Testing, porque sobe a API sem outbox transacional.");

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }
}
