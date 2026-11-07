namespace OficinaMecanica.Domain.Exceptions;

// Optei por nao herdar de DomainException: conflito de estado (a OS nao esta onde a operacao
// pressupoe) vira 409, enquanto violacao de regra de negocio vira 400, e o tratamento de
// excecoes fica sem ambiguidade de ordem de captura.
public class ConflitoException : Exception
{
    public ConflitoException(string mensagem) : base(mensagem) { }
}
