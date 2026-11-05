using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evently.Common.Infrastructure.Outbox;

public sealed class DeadLetteredOutboxMessageConfiguration : IEntityTypeConfiguration<DeadLetteredOutboxMessage>
{
    public void Configure(EntityTypeBuilder<DeadLetteredOutboxMessage> builder)
    {
        builder.ToTable("dead_lettered_outbox_messages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Content).HasColumnType("jsonb");
    }
}
