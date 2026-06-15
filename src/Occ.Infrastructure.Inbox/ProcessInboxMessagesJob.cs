using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Occ.Infrastructure.Correlation;
using Occ.SharedKernal;
using Quartz;

namespace Occ.Infrastructure.Inbox;

[DisallowConcurrentExecution]
public sealed class ProcessInboxMessagesJob(
    IServiceScopeFactory serviceScopeFactory,
    IOptionsMonitor<InboxOptions> options,
    IDateTimeProvider dateTimeProvider,
    ICorrelationAccessor correlationAccessor,
    ILogger<ProcessInboxMessagesJob> logger)
    : IJob
{
    private InboxOptions Options => options.CurrentValue;

    private const string SelectSql = $"""
        SELECT
            id AS Id,
            message_id AS MessageId,
            message_type AS MessageType,
            payload AS Payload,
            correlation_id AS CorrelationId,
            occurred_on_utc AS OccurredOnUtc,
            processed_on_utc AS ProcessedOnUtc,
            retry_count AS RetryCount,
            error AS Error,
            next_retry_utc AS NextRetryUtc
        FROM {InboxMessageConfiguration.Schema}.{InboxMessageConfiguration.Table}
        WHERE processed_on_utc IS NULL
          AND (next_retry_utc IS NULL OR next_retry_utc <= @UtcNow)
        ORDER BY occurred_on_utc
        LIMIT @BatchSize
        FOR UPDATE SKIP LOCKED
        """;

    private const string SuccessSql = $"""
        UPDATE {InboxMessageConfiguration.Schema}.{InboxMessageConfiguration.Table}
        SET processed_on_utc = @ProcessedOnUtc,
            error = NULL,
            next_retry_utc = NULL
        WHERE id = @Id
        """;

    private const string FailureSql = $"""
        UPDATE {InboxMessageConfiguration.Schema}.{InboxMessageConfiguration.Table}
        SET retry_count = @RetryCount,
            error = @Error,
            processed_on_utc = @ProcessedOnUtc,
            next_retry_utc = @NextRetryUtc
        WHERE id = @Id
        """;

    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Processing inbox messages...");

        using var scope = serviceScopeFactory.CreateScope();
        var sqlConnectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var handlers = scope.ServiceProvider
            .GetServices<IInboxMessageHandler>()
            .ToDictionary(h => h.MessageType);

        using var connection = sqlConnectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var messages = (await connection.QueryAsync<InboxMessageDto>(
                SelectSql,
                new { dateTimeProvider.UtcNow, Options.BatchSize },
                transaction)).ToList();

            if (messages.Count == 0)
            {
                logger.LogDebug("No inbox messages to process");
                transaction.Commit();
                return;
            }

            logger.LogInformation("Processing {Count} inbox messages", messages.Count);

            foreach (var message in messages)
                await ProcessMessageAsync(message, handlers, transaction, context.CancellationToken);

            transaction.Commit();
            logger.LogInformation("Finished processing inbox messages batch");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing inbox messages batch, rolling back transaction");
            transaction.Rollback();
            throw;
        }
    }

    private async Task ProcessMessageAsync(
        InboxMessageDto message,
        Dictionary<string, IInboxMessageHandler> handlers,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var correlationId = message.CorrelationId ?? Guid.CreateVersion7().ToString("n");
        using var correlationScope = correlationAccessor.BeginScope(correlationId);

        try
        {
            if (!handlers.TryGetValue(message.MessageType, out var handler))
            {
                logger.LogWarning(
                    "No handler registered for message type {MessageType}. MessageId: {MessageId}",
                    message.MessageType, message.MessageId);

                await MarkFailedAsync(transaction, message.Id,
                    $"No handler registered for message type: {message.MessageType}",
                    message.RetryCount);
                return;
            }

            logger.LogDebug("Processing message {MessageId} of type {MessageType}",
                message.MessageId, message.MessageType);

            var result = await handler.HandleAsync(message.Payload ?? string.Empty, cancellationToken);

            if (result.IsSuccess)
            {
                await MarkSuccessAsync(transaction, message.Id);
                logger.LogInformation("Successfully processed message {MessageId} of type {MessageType}",
                    message.MessageId, message.MessageType);
            }
            else
            {
                logger.LogWarning("Failed to process message {MessageId}: {Error}",
                    message.MessageId, result.Error.Description);
                await MarkFailedAsync(transaction, message.Id, result.Error.Description, message.RetryCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while processing message {MessageId} of type {MessageType}",
                message.MessageId, message.MessageType);
            await MarkFailedAsync(transaction, message.Id, ex.Message, message.RetryCount);
        }
    }

    private async Task MarkSuccessAsync(IDbTransaction transaction, Guid id) =>
        await transaction.Connection!.ExecuteAsync(
            SuccessSql,
            new { Id = id, ProcessedOnUtc = dateTimeProvider.UtcNow },
            transaction);

    private async Task MarkFailedAsync(
        IDbTransaction transaction,
        Guid id,
        string error,
        int currentRetryCount)
    {
        var newRetryCount = currentRetryCount + 1;
        DateTime? processedOnUtc = null;
        DateTime? nextRetryUtc = null;

        if (newRetryCount >= Options.MaxRetryCount)
        {
            processedOnUtc = dateTimeProvider.UtcNow;
        }
        else
        {
            var delay = TimeSpan.FromSeconds(Options.RetryDelayInSeconds * Math.Pow(2, newRetryCount - 1));
            nextRetryUtc = dateTimeProvider.UtcNow.Add(delay);
        }

        await transaction.Connection!.ExecuteAsync(
            FailureSql,
            new { Id = id, RetryCount = newRetryCount, Error = error, ProcessedOnUtc = processedOnUtc, NextRetryUtc = nextRetryUtc },
            transaction);
    }
}