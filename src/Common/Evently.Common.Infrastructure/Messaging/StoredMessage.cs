namespace Evently.Common.Infrastructure.Messaging;

public sealed record StoredMessage(
    Guid Id,
    string Type,
    string Content,
    int RetryCount);
