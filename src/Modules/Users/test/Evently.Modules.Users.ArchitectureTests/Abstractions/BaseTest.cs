using System.Reflection;
using Evently.Modules.Users.Application;
using Evently.Modules.Users.Domain.Users;
using Evently.Modules.Users.Infrastructure;

namespace Evently.Modules.Users.ArchitectureTests.Abstractions;

#pragma warning disable CA1515
public abstract class BaseTest
#pragma warning restore CA1515
{
    protected static readonly Assembly ApplicationAssembly = typeof(AssemblyReference).Assembly;

    protected static readonly Assembly DomainAssembly = typeof(User).Assembly;

    protected static readonly Assembly InfrastructureAssembly = typeof(UsersModule).Assembly;

    protected static readonly Assembly PresentationAssembly = typeof(Users.Presentation.AssemblyReference).Assembly;
}
