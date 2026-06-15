using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Occ.Infrastructure.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public const string Schema = "message";
    public const string Table = "outbox_messages";

    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable(Table, Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.MessageType).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.Property(x => x.RoutingKey).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.OccurredOnUtc).IsRequired();

        builder.HasIndex(x => new { x.ProcessedOnUtc, x.NextRetryUtc });

        //TODO: Postgres'e özel buraya bir çözüm bul!
        builder.Property<uint>("Version").IsRowVersion();
    }
}