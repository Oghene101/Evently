using Evently.Common.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evently.Common.Infrastructure.Inbox;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Content).HasMaxLength(2000).HasColumnType("jsonb");

        builder.Property(o => o.Lane)
            .HasMaxLength(50)
            .HasDefaultValue(MessageLanes.Default)
            .IsRequired();

        builder.HasIndex(o => new { o.Lane, o.OccurredOnUtc })
            .HasFilter("processed_on_utc IS NULL")
            .HasDatabaseName("ix_inbox_messages_unprocessed");
    }
}
