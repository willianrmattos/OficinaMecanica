using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.Behaviors;
using OficinaMecanica.Application.Commands.AprovarOrcamento;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Tests;

public class DependencyInjectionTests
{
    private record ComandoSemRetornoDeTeste(string Nome) : IRequest;

    private class ComandoSemRetornoDeTesteValidator : AbstractValidator<ComandoSemRetornoDeTeste>
    {
        public ComandoSemRetornoDeTesteValidator()
        {
            RuleFor(x => x.Nome).NotEmpty().WithMessage("O nome é obrigatório.");
        }
    }

    private class ComandoSemRetornoDeTesteHandler : IRequestHandler<ComandoSemRetornoDeTeste>
    {
        private readonly List<string> _executados;

        public ComandoSemRetornoDeTesteHandler(List<string> executados) => _executados = executados;

        public Task Handle(ComandoSemRetornoDeTeste request, CancellationToken cancellationToken)
        {
            _executados.Add(request.Nome);
            return Task.CompletedTask;
        }
    }

    private readonly List<string> _executados = new();

    private ServiceProvider CriarProvedor()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new Mock<IOrdemDeServicoRepository>().Object);
        services.AddSingleton(new Mock<IIntegrationEventPublisher>().Object);
        services.AddSingleton(new Mock<IUnitOfWork>().Object);

        // O assembly da Application nao tem nenhum command sem retorno com validator, entao o caso fica
        // registrado aqui, no mesmo container e com o mesmo pipeline que a aplicacao usa.
        services.AddSingleton(_executados);
        services.AddTransient<IRequestHandler<ComandoSemRetornoDeTeste>, ComandoSemRetornoDeTesteHandler>();
        services.AddSingleton<IValidator<ComandoSemRetornoDeTeste>, ComandoSemRetornoDeTesteValidator>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddApplication_CommandSemRetornoInvalido_DeveSerBarradoPeloPipelineAntesDoHandler()
    {
        using var provedor = CriarProvedor();
        var mediator = provedor.GetRequiredService<IMediator>();

        var act = () => mediator.Send(new ComandoSemRetornoDeTeste(""));

        var excecao = await act.Should().ThrowAsync<ValidationException>();
        excecao.Which.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("O nome é obrigatório.");
        _executados.Should().BeEmpty();
    }

    [Fact]
    public async Task AddApplication_CommandSemRetornoValido_DeveChegarAoHandler()
    {
        using var provedor = CriarProvedor();
        var mediator = provedor.GetRequiredService<IMediator>();

        await mediator.Send(new ComandoSemRetornoDeTeste("Ana"));

        _executados.Should().Equal("Ana");
    }

    [Fact]
    public async Task AddApplication_CommandComRetornoInvalido_DeveSerBarradoPeloPipelineAntesDoHandler()
    {
        using var provedor = CriarProvedor();
        var mediator = provedor.GetRequiredService<IMediator>();

        var act = () => mediator.Send(new AprovarOrcamentoCommand(Guid.Empty));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public void AddApplication_TodoCommandSemRetornoDoAssembly_DevePassarPeloValidationBehavior()
    {
        using var provedor = CriarProvedor();
        var commandsSemRetorno = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IRequest).IsAssignableFrom(t))
            .ToList();

        commandsSemRetorno.Should().NotBeEmpty();
        foreach (var command in commandsSemRetorno)
        {
            var behaviors = provedor
                .GetServices(typeof(IPipelineBehavior<,>).MakeGenericType(command, typeof(Unit)))
                .Select(b => b!.GetType());

            behaviors.Should().Contain(
                typeof(ValidationBehavior<,>).MakeGenericType(command, typeof(Unit)),
                because: $"o command {command.Name} nao tem retorno e a validacao precisa rodar para ele");
        }
    }
}
