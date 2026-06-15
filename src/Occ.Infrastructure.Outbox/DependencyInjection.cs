using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Occ.Infrastructure.Outbox;

public static class DependencyInjection
{
    public static IServiceCollection AddOutboxProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OutboxOptions>(options =>
            configuration.GetSection(OutboxOptions.SectionName).Bind(options));

        services.AddQuartz();
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        services.ConfigureOptions<ProcessOutboxMessagesJobSetup>();

        return services;
    }
}