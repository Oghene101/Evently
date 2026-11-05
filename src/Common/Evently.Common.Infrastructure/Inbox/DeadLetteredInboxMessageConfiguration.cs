using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evently.Common.Infrastructure.Inbox;

public sealed class DeadLetteredInboxMessageConfiguration : IEntityTypeConfiguration<DeadLetteredInboxMessage>
{
    public void Configure(EntityTypeBuilder<DeadLetteredInboxMessage> builder)
    {
        builder.ToTable("dead_lettered_inbox_messages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Content).HasColumnType("jsonb");
    }
}
