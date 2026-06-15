using Serilog.Context;

namespace Occ.Infrastructure.Correlation;

public sealed class CorrelationAccessor : ICorrelationAccessor
{

    public const string LogPropertyName = "correlation.id";

    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();
    
    public string CorrelationId => CurrentCorrelationId.Value ?? GenerateCorrelationId();
    
    public IDisposable BeginScope(string correlationId)
    {
        var previousValue = CurrentCorrelationId.Value;
        CurrentCorrelationId.Value = correlationId;

        var logContextDisposable = LogContext.PushProperty(LogPropertyName, correlationId);

        return new CorrelationScope(previousValue, logContextDisposable);
    }

    private static string GenerateCorrelationId()
    {
        var newId = Guid.CreateVersion7().ToString("n");
        CurrentCorrelationId.Value = newId;
        return newId;
    }

    private sealed class CorrelationScope : IDisposable
    {
        private readonly string? _previousValue;
        private readonly IDisposable _logContextDisposable;
        private bool _disposed;

        public CorrelationScope(string? previousValue, IDisposable logContextDisposable)
        {
            _previousValue = previousValue;
            _logContextDisposable = logContextDisposable;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CurrentCorrelationId.Value = _previousValue;
            _logContextDisposable.Dispose();
        }
    }
}