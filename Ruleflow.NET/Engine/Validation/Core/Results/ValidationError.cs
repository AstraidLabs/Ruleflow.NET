using Ruleflow.NET.Engine.Validation.Enums;
using System.Collections.Generic;

namespace Ruleflow.NET.Engine.Validation.Core.Results
{
    public class ValidationError
    {
        public string Message { get; }
        public ValidationSeverity Severity { get; }
        public string? Code { get; }
        public string? Path { get; }
        public IReadOnlyDictionary<string, object?> Metadata { get; }
        public object? Context { get; }

        public ValidationError(
            string message,
            ValidationSeverity severity,
            string? code = null,
            object? context = null,
            string? path = null,
            IReadOnlyDictionary<string, object?>? metadata = null)
        {
            Message = message ?? throw new System.ArgumentNullException(nameof(message));
            Severity = severity;
            Code = code;
            Context = context;
            Path = path;
            Metadata = metadata ?? new Dictionary<string, object?>();
        }
    }
}
