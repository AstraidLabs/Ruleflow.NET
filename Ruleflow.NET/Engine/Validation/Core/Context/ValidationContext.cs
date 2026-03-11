using System.Collections.Concurrent;
using System.Threading;

namespace Ruleflow.NET.Engine.Validation.Core.Context
{
    public class RuleExecutionResult
    {
        public bool Success { get; set; }
    }

    /// <summary>
    /// Determines how <see cref="ValidationContext.Instance"/> is resolved.
    /// </summary>
    public enum ValidationContextMode
    {
        /// <summary>
        /// A single global instance is shared across all validation runs (legacy behavior).
        /// </summary>
        GlobalSingleton,

        /// <summary>
        /// Each validation run gets its own context via <see cref="AsyncLocal{T}"/>.
        /// This is the default and recommended mode.
        /// </summary>
        ScopedAsyncFlow
    }

    public class ValidationContext
    {
        /// <summary>
        /// Controls whether <see cref="Instance"/> returns a global singleton or a per-run scoped context.
        /// </summary>
        public static ValidationContextMode Mode { get; set; } = ValidationContextMode.ScopedAsyncFlow;

        private static readonly AsyncLocal<ValidationContext?> _current = new();

        /// <summary>
        /// Gets or sets the current async-local validation context.
        /// </summary>
        internal static ValidationContext? Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }

        private static readonly ValidationContext _global = new ValidationContext();

        /// <summary>
        /// Returns the active <see cref="ValidationContext"/> for the current execution flow.
        /// In <see cref="ValidationContextMode.GlobalSingleton"/> mode the global instance is always returned.
        /// In <see cref="ValidationContextMode.ScopedAsyncFlow"/> mode the async-local context is returned
        /// if one has been established via <see cref="ValidationContextScope.Begin"/>, otherwise the global fallback.
        /// </summary>
        public static ValidationContext Instance =>
            Mode == ValidationContextMode.GlobalSingleton
                ? _global
                : _current.Value ?? _global;

        public ConcurrentDictionary<string, object?> Properties { get; } = new(System.StringComparer.Ordinal);
        public ConcurrentDictionary<string, RuleExecutionResult> RuleResults { get; } = new(System.StringComparer.Ordinal);

        public void Clear()
        {
            Properties.Clear();
            RuleResults.Clear();
        }
    }
}
