using System.Threading;
using System.Threading.Tasks;
using Ruleflow.NET.Engine.Validation.Enums;
using Ruleflow.NET.Engine.Validation.Interfaces;

namespace Ruleflow.NET.Engine.Validation.Core.Base
{
    public sealed class SyncToAsyncValidationRuleAdapter<T> : IAsyncValidationRule<T>
    {
        private readonly IValidationRule<T> _inner;

        public SyncToAsyncValidationRuleAdapter(IValidationRule<T> inner)
        {
            _inner = inner;
        }

        public string Id => _inner.Id;
        public int Priority => _inner.Priority;
        public ValidationSeverity Severity => _inner.Severity;

        public Task ValidateAsync(T input, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _inner.Validate(input);
            return Task.CompletedTask;
        }
    }
}
