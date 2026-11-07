namespace OficinaMecanica.Application.DTOs;

// Resposta de 202: a decisao foi aceita e publicada para a saga, mas o status da OS ainda e o de
// antes. O numero permite acompanhar pela consulta publica por numero.
public record SolicitacaoAceitaDto(Guid OrdemDeServicoId, string Numero, string Status);
