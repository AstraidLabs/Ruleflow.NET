using System;
using System.Threading;
using System.Threading.Tasks;
using Ruleflow.NET.Engine.Validation.Enums;
using Ruleflow.NET.Engine.Validation.Interfaces;

namespace Ruleflow.NET.Engine.Validation.Core.Base
{
    public class AsyncActionValidationRule<T> : IAsyncValidationRule<T>
    {
        private readonly Func<T, CancellationToken, Task> _action;

        public string Id { get; }
        public int Priority { get; private set; }
        public ValidationSeverity Severity { get; private set; } = ValidationSeverity.Error;

        public AsyncActionValidationRule(string id, Func<T, CancellationToken, Task> action)
        {
            Id = id;
            _action = action;
        }

        public void SetPriority(int priority) => Priority = priority;
        public void SetSeverity(ValidationSeverity severity) => Severity = severity;

        public Task ValidateAsync(T input, CancellationToken cancellationToken = default)
        {
            return _action(input, cancellationToken);
        }
    }
}
