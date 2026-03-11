namespace Ruleflow.NET.Engine.Validation.Core.Context
{
    /// <summary>
    /// Default implementation of <see cref="IValidationContextAccessor"/> that delegates
    /// to <see cref="ValidationContext.Instance"/>.
    /// </summary>
    public sealed class ValidationContextAccessor : IValidationContextAccessor
    {
        public ValidationContext Current => ValidationContext.Instance;
    }
}
