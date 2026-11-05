namespace Evently.Common.Infrastructure.UnitTests.Abstractions;

[CollectionDefinition(nameof(MessagingDatabaseCollection))]
public sealed class MessagingDatabaseCollection : ICollectionFixture<MessagingDatabase>;
