using System;

namespace Ruleflow.NET.Engine.Validation.Core.Context
{
    /// <summary>
    /// Creates a scoped <see cref="ValidationContext"/> for the duration of a validation run.
    /// Disposing the scope restores the previous context.
    /// </summary>
    public sealed class ValidationContextScope : IDisposable
    {
        private readonly ValidationContext? _prior;
        private readonly bool _isNoOp;

        private ValidationContextScope(ValidationContext? prior, bool isNoOp)
        {
            _prior = prior;
            _isNoOp = isNoOp;
        }

        /// <summary>
        /// Begins a new validation context scope.
        /// In <see cref="ValidationContextMode.GlobalSingleton"/> mode a no-op scope is returned.
        /// Otherwise a fresh <see cref="ValidationContext"/> is set on the async-local store.
        /// </summary>
        public static ValidationContextScope Begin()
        {
            if (ValidationContext.Mode == ValidationContextMode.GlobalSingleton)
            {
                return new ValidationContextScope(null, isNoOp: true);
            }

            var prior = ValidationContext.Current;
            ValidationContext.Current = new ValidationContext();
            return new ValidationContextScope(prior, isNoOp: false);
        }

        public void Dispose()
        {
            if (!_isNoOp)
            {
                ValidationContext.Current = _prior;
            }
        }
    }
}
