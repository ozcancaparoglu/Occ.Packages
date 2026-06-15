using Microsoft.Extensions.Options;
using Quartz;

namespace Occ.Infrastructure.Inbox;

internal sealed class ProcessInboxMessagesJobSetup(IOptions<InboxOptions> inboxOptions)
    : IConfigureOptions<QuartzOptions>
{
    private readonly InboxOptions _inboxOptions = inboxOptions.Value;

    public void Configure(QuartzOptions options)
    {
        const string jobName = nameof(ProcessInboxMessagesJob);
        options
            .AddJob<ProcessInboxMessagesJob>(configure => configure
                .WithIdentity(jobName)
                .StoreDurably())
            .AddTrigger(configure =>
                configure
                    .ForJob(jobName)
                    .WithSimpleSchedule(schedule =>
                        schedule.WithIntervalInSeconds(_inboxOptions.IntervalInSeconds).RepeatForever()));
    }
}