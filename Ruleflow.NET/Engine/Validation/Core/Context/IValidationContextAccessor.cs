namespace Ruleflow.NET.Engine.Validation.Core.Context
{
    /// <summary>
    /// Provides access to the current <see cref="ValidationContext"/> without directly injecting it.
    /// Useful when the consumer is registered as a singleton but needs the per-run context.
    /// </summary>
    public interface IValidationContextAccessor
    {
        ValidationContext Current { get; }
    }
}
