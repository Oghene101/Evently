namespace Evently.Common.Infrastructure.Messaging;

public sealed record PurgedMessages(
    long Messages,
    long Consumers);
