using System.Reflection;

namespace Evently.Common.Infrastructure.Messaging;

public sealed record InboxDefinition(
    string ModuleName,
    string Schema,
    Assembly HandlersAssembly,
    IReadOnlyList<string> Lanes);
