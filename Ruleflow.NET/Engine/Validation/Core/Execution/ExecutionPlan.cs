using System;
using System.Collections.Generic;
using System.Linq;
using Ruleflow.NET.Engine.Validation.Core.Base;

namespace Ruleflow.NET.Engine.Validation.Core.Execution
{
    public sealed class ExecutionPlan<T>
    {
        public IReadOnlyList<ExecutionPlanStep<T>> Steps { get; }
        public IReadOnlyList<IReadOnlyList<ExecutionPlanStep<T>>> Stages { get; }

        public ExecutionPlan(IEnumerable<ExecutionPlanStep<T>> steps, IEnumerable<IReadOnlyList<ExecutionPlanStep<T>>>? stages = null)
        {
            Steps = steps.ToArray();
            Stages = stages?.ToArray() ?? new[] { Steps };
        }

        public static ExecutionPlan<T> CreateSequential(IEnumerable<Interfaces.IValidationRule<T>> rules)
        {
            var steps = rules
                .OrderByDescending(r => r.Priority)
                .Select(r => new ExecutionPlanStep<T>(r.Id, r.Priority, r.Severity, r.Validate))
                .ToArray();

            return new ExecutionPlan<T>(steps, new[] { steps });
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
            var stageRules = new List<IReadOnlyList<Interfaces.IValidationRule<T>>>();
            while (queue.TryDequeue(out var current, out _))
            {
                var currentStage = new List<Interfaces.IValidationRule<T>> { current };
                while (queue.TryDequeue(out var sameStageRule, out _))
                {
                    currentStage.Add(sameStageRule);
                }

                var orderedStage = currentStage
                    .OrderByDescending(r => r.Priority)
                    .ThenBy(r => r.Id, StringComparer.Ordinal)
                    .ToArray();

                stageRules.Add(orderedStage);
                ordered.AddRange(orderedStage);

                foreach (var stageRule in orderedStage)
                {
                    foreach (var next in outgoing[stageRule.Id])
                    {
                        incoming[next]--;
                        if (incoming[next] == 0)
                        {
                            queue.Enqueue(map[next], -map[next].Priority);
                        }
                    }
                }
            }

            if (ordered.Count != map.Count)
            {
                throw new InvalidOperationException("Circular dependency detected");
            }

            var stepMap = ordered.Select(r =>
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
            }).ToDictionary(s => s.RuleId, StringComparer.Ordinal);

            var steps = ordered.Select(r => stepMap[r.Id]).ToArray();
            var stages = stageRules
                .Select(stage => (IReadOnlyList<ExecutionPlanStep<T>>)stage.Select(rule => stepMap[rule.Id]).ToArray())
                .ToArray();

            return new ExecutionPlan<T>(steps, stages);
        }
    }
}
