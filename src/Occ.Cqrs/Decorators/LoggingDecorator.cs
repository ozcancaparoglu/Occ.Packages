using Microsoft.Extensions.Logging;
using Occ.Cqrs.Messaging;
using Occ.SharedKernal;
using Serilog.Context;

namespace Occ.Cqrs.Decorators;

internal static class LoggingDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            string name = typeof(TCommand).Name;
            logger.LogInformation("Processing command {Command}", name);

            var result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess)
                logger.LogInformation("Completed command {Command}", name);
            else
                using (LogContext.PushProperty("Error", result.Error, true))
                    logger.LogError("Completed command {Command} with error", name);

            return result;
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandBaseHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            string name = typeof(TCommand).Name;
            logger.LogInformation("Processing command {Command}", name);

            var result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess)
                logger.LogInformation("Completed command {Command}", name);
            else
                using (LogContext.PushProperty("Error", result.Error, true))
                    logger.LogError("Completed command {Command} with error", name);

            return result;
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            string name = typeof(TQuery).Name;
            logger.LogInformation("Processing query {Query}", name);

            var result = await innerHandler.Handle(query, cancellationToken);

            if (result.IsSuccess)
                logger.LogInformation("Completed query {Query}", name);
            else
                using (LogContext.PushProperty("Error", result.Error, true))
                    logger.LogError("Completed query {Query} with error", name);

            return result;
        }
    }
}