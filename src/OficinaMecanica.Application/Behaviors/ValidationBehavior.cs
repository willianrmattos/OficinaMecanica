using FluentValidation;
using MediatR;

namespace OficinaMecanica.Application.Behaviors;

// Optei por restringir TRequest a notnull (a mesma restricao de IPipelineBehavior) em vez de IRequest<TResponse>:
// os commands sem retorno implementam IRequest, que no MediatR 12 nao e IRequest<Unit>, e com a restricao mais
// estreita o container nunca fecharia este behavior para eles e a validacao jamais rodaria.
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next();

        // Um contexto por validator: o contexto acumula as falhas e, compartilhado, repetiria as do primeiro
        // validator no resultado do segundo (alem de ser mutado em paralelo pelo Task.WhenAll).
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken))
        );

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
