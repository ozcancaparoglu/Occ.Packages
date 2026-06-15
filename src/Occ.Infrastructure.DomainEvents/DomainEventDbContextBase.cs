using Microsoft.EntityFrameworkCore;
using Occ.SharedKernal;

namespace Occ.Infrastructure.DomainEvents;

// <summary>
/// EF Core DbContext base that dispatches domain events after SaveChangesAsync.
/// Inherit from this instead of DbContext to get automatic event dispatch.
/// </summary>
public abstract class DomainEventDbContextBase(
    DbContextOptions options,
    IDomainEventDispatcher dispatcher)
    : DbContext(options)
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker
            .Entries<Entity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var entity in ChangeTracker.Entries<Entity>().Select(e => e.Entity))
            entity.ClearDomainEvents();

        int result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
            await dispatcher.DispatchAsync(domainEvent, cancellationToken);

        return result;
    }
}
