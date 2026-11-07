using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// Guarda o que a aplicacao e o MassTransit logam em Warning ou acima, para os testes afirmarem que nenhuma
// excecao inesperada (ex.: InvalidOperationException) foi engolida pelo retry.
public sealed class LoggerCapturador : ILoggerProvider
{
    private readonly ConcurrentQueue<RegistroDeLog> _registros = new();

    public IReadOnlyList<RegistroDeLog> Registros => _registros.ToArray();

    public IEnumerable<Exception> Excecoes() =>
        _registros.Select(r => r.Excecao).Where(e => e is not null).Select(e => e!);

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger : ILogger
    {
        private readonly LoggerCapturador _pai;
        private readonly string _categoria;

        public Logger(LoggerCapturador pai, string categoria)
        {
            _pai = pai;
            _categoria = categoria;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                _pai._registros.Enqueue(new RegistroDeLog(logLevel, _categoria, formatter(state, exception), exception));
        }
    }
}

public sealed record RegistroDeLog(LogLevel Nivel, string Categoria, string Mensagem, Exception? Excecao);
