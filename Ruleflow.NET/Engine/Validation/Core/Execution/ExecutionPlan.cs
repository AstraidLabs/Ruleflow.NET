using System;
using System.Collections.Generic;
using System.Linq;
using Ruleflow.NET.Engine.Validation.Core.Base;

namespace Ruleflow.NET.Engine.Validation.Core.Execution
{
    public sealed class ExecutionPlan<T>
    {
        public IReadOnlyList<ExecutionPlanStep<T>> Steps { get; }

        public ExecutionPlan(IEnumerable<ExecutionPlanStep<T>> steps)
        {
            Steps = steps.ToArray();
        }

        public static ExecutionPlan<T> CreateSequential(IEnumerable<Interfaces.IValidationRule<T>> rules)
        {
            var steps = rules
                .OrderByDescending(r => r.Priority)
                .Select(r => new ExecutionPlanStep<T>(r.Id, r.Priority, r.Severity, r.Validate))
                .ToArray();

            return new ExecutionPlan<T>(steps);
        }

        public static ExecutionPlan<T> CreateDependencyAware(IEnumerable<Interfaces.IValidationRule<T>> rules)
        {
            var map = rules.ToDictionary(r => r.Id);
            var incoming = map.Values.ToDictionary(
                r => r.Id,
                r => r is DependentValidationRule<T> dependent
                    ? dependent.Dependencies.Count(d => map.ContainsKey(d))
                    : 0);

            var outgoing = map.Keys.ToDictionary(id => id, _ => new List<string>());
            foreach (var dependent in map.Values.OfType<DependentValidationRule<T>>())
            {
                foreach (var dependencyId in dependent.Dependencies.Where(map.ContainsKey))
                {
                    outgoing[dependencyId].Add(dependent.Id);
                }
            }

            var queue = new PriorityQueue<Interfaces.IValidationRule<T>, int>();
            foreach (var rule in map.Values.Where(r => incoming[r.Id] == 0))
            {
                queue.Enqueue(rule, -rule.Priority);
            }

            var ordered = new List<Interfaces.IValidationRule<T>>(map.Count);
            while (queue.TryDequeue(out var current, out _))
            {
                ordered.Add(current);
                foreach (var next in outgoing[current.Id])
                {
                    incoming[next]--;
                    if (incoming[next] == 0)
                    {
                        queue.Enqueue(map[next], -map[next].Priority);
                    }
                }
            }

            if (ordered.Count != map.Count)
            {
                throw new InvalidOperationException("Circular dependency detected");
            }

            var steps = ordered.Select(r =>
            {
                if (r is DependentValidationRule<T> dependent)
                {
                    var deps = dependent.Dependencies.Where(map.ContainsKey).ToArray();
                    return new ExecutionPlanStep<T>(
                        r.Id,
                        r.Priority,
                        r.Severity,
                        r.Validate,
                        deps,
                        dependent.DependencyType);
                }

                return new ExecutionPlanStep<T>(r.Id, r.Priority, r.Severity, r.Validate);
            });

            return new ExecutionPlan<T>(steps);
        }
    }
}
