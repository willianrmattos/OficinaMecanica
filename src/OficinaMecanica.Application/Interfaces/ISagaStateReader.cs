namespace OficinaMecanica.Application.Interfaces;

// Leitura (somente leitura) do ultimo estado conhecido da saga da OS, que vive no banco do proprio OS Service. E o que a
// consulta sincrona a Execucao devolve quando ela esta indisponivel: a Application nao conhece a entidade da saga, so este retrato.
public interface ISagaStateReader
{
    Task<EstadoConhecidoDaSaga?> ObterAsync(Guid ordemDeServicoId, CancellationToken cancellationToken = default);
}

public sealed record EstadoConhecidoDaSaga(string Estado, DateTimeOffset AtualizadaEm);
