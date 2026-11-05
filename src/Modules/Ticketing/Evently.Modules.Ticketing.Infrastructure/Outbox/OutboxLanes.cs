using Evently.Common.Domain;
using Evently.Common.Infrastructure.Messaging;
using Evently.Modules.Ticketing.Domain.Payments;

namespace Evently.Modules.Ticketing.Infrastructure.Outbox;

internal static class OutboxLanes
{
    // Refunds call the payment provider, so a slow or failing provider holds up nothing else.
    public const string Payments = "Payments";

    public const string Default = MessageLanes.Default;

    public static IReadOnlyList<string> All { get; } = [Payments, Default];

    public static string Of(IDomainEvent domainEvent) => domainEvent switch
    {
        PaymentRefundedDomainEvent or PaymentPartiallyRefundedDomainEvent => Payments,
        _ => Default
    };
}
