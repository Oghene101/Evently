using Evently.Common.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evently.Common.Infrastructure.Inbox;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        // A queue table: claims update rows in place, so room on each page keeps those updates HOT, and vacuum runs
        // at a few percent of dead rows instead of the default 20%.
        builder.HasStorageParameter("fillfactor", 80)
            .HasStorageParameter("autovacuum_vacuum_scale_factor", 0.02)
            .HasStorageParameter("autovacuum_analyze_scale_factor", 0.02);

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Content).HasMaxLength(2000).HasColumnType("jsonb");

        builder.Property(o => o.Lane)
            .HasMaxLength(50)
            .HasDefaultValue(MessageLanes.Default)
            .IsRequired();

        // Unaffected by a claim, so a lease never moves the row in the index; retry times change only on failure.
        builder.Property(o => o.AvailableOnUtc)
            .HasComputedColumnSql("COALESCE(next_retry_on_utc, occurred_on_utc)", stored: true);

        // Ordered by availability, a claim stops at the first message not yet due instead of reading past every
        // message waiting on a retry.
        builder.HasIndex(o => new { o.Lane, o.AvailableOnUtc })
            .HasFilter("processed_on_utc IS NULL")
            .HasDatabaseName("ix_inbox_messages_unprocessed");

        builder.HasIndex(o => o.ProcessedOnUtc)
            .HasFilter("processed_on_utc IS NOT NULL")
            .HasDatabaseName("ix_inbox_messages_processed");
    }
}
