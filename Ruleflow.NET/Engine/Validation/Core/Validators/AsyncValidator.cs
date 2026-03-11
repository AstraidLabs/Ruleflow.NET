using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ruleflow.NET.Engine.Validation.Core.Context;
using Ruleflow.NET.Engine.Validation.Core.Results;
using Ruleflow.NET.Engine.Validation.Interfaces;

namespace Ruleflow.NET.Engine.Validation.Core.Validators
{
    public class AsyncValidator<T> : IAsyncValidator<T>
    {
        private readonly IReadOnlyList<IAsyncValidationRule<T>> _rules;
        private readonly ILogger<AsyncValidator<T>> _logger;

        public AsyncValidator(IEnumerable<IAsyncValidationRule<T>> rules, ILogger<AsyncValidator<T>>? logger = null)
        {
            _rules = rules.OrderByDescending(r => r.Priority).ToArray();
            _logger = logger ?? NullLogger<AsyncValidator<T>>.Instance;
        }

        public async Task<ValidationResult> CollectValidationResultsAsync(T input, CancellationToken cancellationToken = default)
        {
            using var scope = ValidationContextScope.Begin();
            var context = ValidationContext.Instance;
            var result = new ValidationResult();

            foreach (var rule in _rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await rule.ValidateAsync(input, cancellationToken).ConfigureAwait(false);
                    context.RuleResults[rule.Id] = new RuleExecutionResult { Success = true };
                }
                catch (Exception ex)
                {
                    context.RuleResults[rule.Id] = new RuleExecutionResult { Success = false };
                    result.AddError(ex.Message, rule.Severity, code: rule.Id, path: rule.Id);
                    _logger.LogError(ex, "Rule {RuleId} failed: {Message}", rule.Id, ex.Message);
                }
            }

            return result;
        }

        public async Task<bool> IsValidAsync(T input, CancellationToken cancellationToken = default)
            => (await CollectValidationResultsAsync(input, cancellationToken).ConfigureAwait(false)).IsValid;

        public async Task<ValidationError?> GetFirstErrorAsync(T input, CancellationToken cancellationToken = default)
            => (await CollectValidationResultsAsync(input, cancellationToken).ConfigureAwait(false)).Errors.FirstOrDefault();

        public async Task ValidateOrThrowAsync(T input, CancellationToken cancellationToken = default)
            => (await CollectValidationResultsAsync(input, cancellationToken).ConfigureAwait(false)).ThrowIfInvalid();

        public async Task<ValidationResult> ValidateAndProcessAsync(T input, Func<T, CancellationToken, Task> processingAction, CancellationToken cancellationToken = default)
        {
            var result = await CollectValidationResultsAsync(input, cancellationToken).ConfigureAwait(false);
            if (result.IsValid)
            {
                await processingAction(input, cancellationToken).ConfigureAwait(false);
            }
            return result;
        }

        public async Task ValidateAndExecuteAsync(T input, Func<CancellationToken, Task> successAction, Func<IReadOnlyList<ValidationError>, CancellationToken, Task> failureAction, CancellationToken cancellationToken = default)
        {
            var result = await CollectValidationResultsAsync(input, cancellationToken).ConfigureAwait(false);
            if (result.IsValid)
            {
                await successAction(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await failureAction(result.Errors, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
