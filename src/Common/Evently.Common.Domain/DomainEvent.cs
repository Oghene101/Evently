namespace Evently.Common.Domain;

public abstract class DomainEvent(Guid id, DateTime occuredOnUtc) : IDomainEvent
{
    protected DomainEvent() : this(Guid.CreateVersion7(), DateTime.UtcNow)
    {
    }

    public Guid Id { get; set; } = id;
    public DateTime OccurredOnUtc { get; set; } = occuredOnUtc;
}
