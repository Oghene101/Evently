using Microsoft.Extensions.Options;
using Quartz;

namespace Evently.Common.Infrastructure.Messaging;

internal sealed class ConfigurePurgeProcessedMessagesJob(
    MessageTable table,
    MessageBoxOptions messageBoxOptions) : IConfigureOptions<QuartzOptions>
{
    public void Configure(QuartzOptions options)
    {
        string jobName = $"{typeof(PurgeProcessedMessagesJob).FullName}.{table.QualifiedMessagesTable}";

        var purgeJobData = new JobDataMap
        {
            [PurgeProcessedMessagesJob.MessageTableKey] = table,
            [PurgeProcessedMessagesJob.RetentionDaysKey] = messageBoxOptions.RetentionDays
        };

        options
            .AddJob<PurgeProcessedMessagesJob>(configure => configure
                .WithIdentity(jobName)
                .UsingJobData(purgeJobData))
            .AddTrigger(configure =>
                configure
                    .ForJob(jobName)
                    .StartNow()
                    .WithSimpleSchedule(schedule =>
                        schedule.WithIntervalInMinutes(messageBoxOptions.PurgeIntervalMinutes).RepeatForever()));
    }
}
