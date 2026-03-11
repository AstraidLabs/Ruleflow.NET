using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ruleflow.NET.Engine.Validation.Core.Results;

namespace Ruleflow.NET.Engine.Validation.Interfaces
{
    public interface IAsyncValidator<T>
    {
        Task<ValidationResult> CollectValidationResultsAsync(T input, CancellationToken cancellationToken = default);
        Task<bool> IsValidAsync(T input, CancellationToken cancellationToken = default);
        Task<ValidationError?> GetFirstErrorAsync(T input, CancellationToken cancellationToken = default);
        Task ValidateOrThrowAsync(T input, CancellationToken cancellationToken = default);
        Task<ValidationResult> ValidateAndProcessAsync(T input, Func<T, CancellationToken, Task> processingAction, CancellationToken cancellationToken = default);
        Task ValidateAndExecuteAsync(T input, Func<CancellationToken, Task> successAction, Func<IReadOnlyList<ValidationError>, CancellationToken, Task> failureAction, CancellationToken cancellationToken = default);
    }
}
