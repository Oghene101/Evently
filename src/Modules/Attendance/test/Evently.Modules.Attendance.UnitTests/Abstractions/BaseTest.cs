using Bogus;
using Evently.Common.Domain;

namespace Evently.Modules.Attendance.UnitTests.Abstractions;

#pragma warning disable CA1515
public abstract class BaseTest
#pragma warning restore CA1515
{
    protected static readonly Faker Faker = new();

    public static T AssertDomainEventWasPublished<T>(Entity entity)
        where T : IDomainEvent
    {
        T? domainEvent = entity.DomainEvents.OfType<T>().SingleOrDefault();

        return domainEvent ?? throw new Exception($"{typeof(T).Name} was not published");
    }
}
