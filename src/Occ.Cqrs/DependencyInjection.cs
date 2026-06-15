using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Occ.Cqrs.Decorators;
using Occ.Cqrs.Messaging;
using Occ.SharedKernal;

namespace Occ.Cqrs;

public static class DependencyInjection
{
    /// <summary>
    /// Scans <paramref name="assemblies"/> for CQRS handlers and domain event handlers,
    /// applies ValidationDecorator → LoggingDecorator, and registers FluentValidation validators.
    /// </summary>
    public static IServiceCollection AddCqrs(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IInboxMessageHandler<>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime());

        TryDecorate(services, typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
        TryDecorate(services, typeof(ICommandHandler<>), typeof(ValidationDecorator.CommandBaseHandler<>));

        TryDecorate(services, typeof(IQueryHandler<,>), typeof(LoggingDecorator.QueryHandler<,>));
        TryDecorate(services, typeof(ICommandHandler<,>), typeof(LoggingDecorator.CommandHandler<,>));
        TryDecorate(services, typeof(ICommandHandler<>), typeof(LoggingDecorator.CommandBaseHandler<>));

        foreach (var assembly in assemblies)
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }

    private static void TryDecorate(IServiceCollection services, Type serviceType, Type decoratorType)
    {
        try
        {
            services.Decorate(serviceType, decoratorType);
        }
        catch (Scrutor.DecorationException)
        {
            // no registrations for this type — skip
        }
    }
}