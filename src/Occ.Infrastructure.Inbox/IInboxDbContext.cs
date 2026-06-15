using Microsoft.EntityFrameworkCore;

namespace Occ.Infrastructure.Inbox;

/// <summary>
/// Minimal EF Core interface for writing to the inbox_messages table.
/// Implement this alongside your service's IApplicationDbContext.
/// </summary>
public interface IInboxDbContext
{
    DbSet<InboxMessage> InboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}