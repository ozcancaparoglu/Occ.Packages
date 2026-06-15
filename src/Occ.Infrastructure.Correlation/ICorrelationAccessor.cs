namespace Occ.Infrastructure.Correlation;

public interface ICorrelationAccessor
{
    string CorrelationId { get; }
    IDisposable BeginScope(string correlationId);
}