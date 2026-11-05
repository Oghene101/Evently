using Evently.Common.Domain;

namespace Evently.Common.Infrastructure.Messaging;

public static class MessageErrors
{
    // An exception says nothing about whether another attempt can succeed, so it is retried like a dependency.
    public static Error HandlerThrew(Exception exception) => Error.Dependency(
        "Message.HandlerThrew",
        exception.ToString());

    public static Error Unreadable(Exception exception) => Error.Problem(
        "Message.Unreadable",
        exception.Message);
}
