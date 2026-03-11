using System;
using System.Collections.Generic;
using Ruleflow.NET.Engine.Validation.Enums;

namespace Ruleflow.NET.Engine.Validation.Core.Execution
{
    public sealed class ExecutionPlanStep<T>
    {
        public string RuleId { get; }
        public int Priority { get; }
        public ValidationSeverity Severity { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public DependencyType? DependencyType { get; }
        public Action<T> Execute { get; }

        public ExecutionPlanStep(
            string ruleId,
            int priority,
            ValidationSeverity severity,
            Action<T> execute,
            IReadOnlyList<string>? dependencies = null,
            DependencyType? dependencyType = null)
        {
            RuleId = ruleId;
            Priority = priority;
            Severity = severity;
            Execute = execute;
            Dependencies = dependencies ?? Array.Empty<string>();
            DependencyType = dependencyType;
        }
    }
}
