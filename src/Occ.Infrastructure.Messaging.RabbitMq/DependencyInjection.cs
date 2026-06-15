using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Occ.SharedKernal;

namespace Occ.Infrastructure.Messaging.RabbitMq;

public static class DependencyInjection
{
    /// <summary>
    /// Registers RabbitMqPublisher as IMessagePublisher singleton.
    /// Used by outbox processing to publish messages.
    /// </summary>
    public static IServiceCollection AddRabbitMqPublisher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(options =>
            configuration.GetSection(RabbitMqOptions.SectionName).Bind(options));

        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

        return services;
    }

    /// <summary>
    /// Registers RabbitMqConsumer as a hosted service.
    /// Consumes messages from RabbitMQ and writes them to the inbox table.
    /// Requires IInboxDbContext to be registered in DI.
    /// </summary>
    public static IServiceCollection AddRabbitMqConsumer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(options =>
            configuration.GetSection(RabbitMqOptions.SectionName).Bind(options));

        services.AddHostedService<RabbitMqConsumer>();

        return services;
    }

    /// <summary>
    /// Registers both RabbitMqPublisher and RabbitMqConsumer.
    /// Use this when a service both publishes and consumes messages.
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(options =>
            configuration.GetSection(RabbitMqOptions.SectionName).Bind(options));

        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        services.AddHostedService<RabbitMqConsumer>();

        return services;
    }
}