namespace Evently.Common.Infrastructure.Messaging;

public sealed record MessageTable(
    string ModuleName,
    string Schema,
    string MessagesTable,
    string DeadLetteredMessagesTable,
    string ConsumersTable,
    string ConsumerMessageIdColumn)
{
    public static MessageTable Outbox(string moduleName, string schema) => new(
        moduleName,
        schema,
        "outbox_messages",
        "dead_lettered_outbox_messages",
        "outbox_message_consumers",
        "outbox_message_id");

    public static MessageTable Inbox(string moduleName, string schema) => new(
        moduleName,
        schema,
        "inbox_messages",
        "dead_lettered_inbox_messages",
        "inbox_message_consumers",
        "inbox_message_id");

    public string QualifiedMessagesTable => $"{Schema}.{MessagesTable}";

    public string QualifiedDeadLetteredMessagesTable => $"{Schema}.{DeadLetteredMessagesTable}";

    public string QualifiedConsumersTable => $"{Schema}.{ConsumersTable}";
}
