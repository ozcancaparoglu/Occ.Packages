using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Occ.Infrastructure.Inbox;

public static class DependencyInjection
{
    public static IServiceCollection AddInboxProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<InboxOptions>(options =>
            configuration.GetSection(InboxOptions.SectionName).Bind(options));

        services.AddQuartz();
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        services.ConfigureOptions<ProcessInboxMessagesJobSetup>();

        return services;
    }
}