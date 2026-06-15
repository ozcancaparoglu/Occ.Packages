using FluentValidation;
using FluentValidation.Results;
using Occ.Cqrs.Messaging;
using Occ.SharedKernal;

namespace Occ.Cqrs.Decorators;

internal static class ValidationDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var failures = await ValidateAsync(command, validators);
            return failures.Length == 0
                ? await innerHandler.Handle(command, cancellationToken)
                : Result.Failure<TResponse>(CreateValidationError(failures));
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var failures = await ValidateAsync(command, validators);
            return failures.Length == 0
                ? await innerHandler.Handle(command, cancellationToken)
                : Result.Failure(CreateValidationError(failures));
        }
    }

    private static async Task<ValidationFailure[]> ValidateAsync<TCommand>(
        TCommand command,
        IEnumerable<IValidator<TCommand>> validators)
    {
        var enumerable = validators as IValidator<TCommand>[] ?? validators.ToArray();
        
        if (!enumerable.Any())
            return [];

        var context = new ValidationContext<TCommand>(command);

        var results = await Task.WhenAll(enumerable.Select(v => v.ValidateAsync(context)));

        return results
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToArray();
    }

    private static ValidationError CreateValidationError(ValidationFailure[] failures) =>
        new(failures.Select(f => Error.Problem(f.ErrorCode, f.ErrorMessage)).ToArray());
}