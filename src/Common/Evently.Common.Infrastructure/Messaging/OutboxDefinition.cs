using System.Reflection;
using Evently.Common.Domain;

namespace Evently.Common.Infrastructure.Messaging;

public sealed record OutboxDefinition(
    string ModuleName,
    string Schema,
    Assembly HandlersAssembly,
    IReadOnlyList<string> Lanes,
    Func<IDomainEvent, string> LaneOf);
