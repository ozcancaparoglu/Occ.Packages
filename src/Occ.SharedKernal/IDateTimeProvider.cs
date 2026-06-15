namespace Occ.SharedKernal;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}