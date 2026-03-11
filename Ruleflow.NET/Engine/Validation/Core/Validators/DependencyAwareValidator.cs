using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ruleflow.NET.Engine.Validation.Core.Base;
using Ruleflow.NET.Engine.Validation.Core.Context;
using Ruleflow.NET.Engine.Validation.Core.Execution;
using Ruleflow.NET.Engine.Validation.Core.Results;
using Ruleflow.NET.Engine.Validation.Enums;
using Ruleflow.NET.Engine.Validation.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ruleflow.NET.Engine.Validation.Core.Validators
{
    public sealed class RuleExecutionHooks<T>
    {
        public Action<string>? OnRuleStart { get; init; }
        public Action<string>? OnRuleSuccess { get; init; }
        public Action<string, Exception>? OnRuleFailure { get; init; }
        public Action<string>? OnRuleSkipped { get; init; }
    }

    public sealed class DependencyAwareValidatorOptions<T>
    {
        public bool EnableParallelStages { get; set; }
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
        public RuleExecutionHooks<T>? Hooks { get; set; }
    }

    internal static class ValidationTelemetry
    {
        private static readonly ActivitySource ActivitySource = new("Ruleflow.NET.Validation");
        private static readonly Meter Meter = new("Ruleflow.NET.Validation");

        public static readonly Histogram<double> RuleDurationMs = Meter.CreateHistogram<double>(
            "ruleflow.validation.rule.duration.ms",
            unit: "ms",
            description: "Rule execution duration in milliseconds.");

        public static readonly Counter<long> RuleFailures = Meter.CreateCounter<long>(
            "ruleflow.validation.rule.failures",
            unit: "count",
            description: "Number of failed rules.");

        public static readonly Counter<long> RuleSkips = Meter.CreateCounter<long>(
            "ruleflow.validation.rule.skips",
            unit: "count",
            description: "Number of skipped rules due to dependency gate.");

        public static Activity? StartRuleActivity(string ruleId)
        {
            var activity = ActivitySource.StartActivity("Ruleflow.Validation.Rule", ActivityKind.Internal);
            activity?.SetTag("rule.id", ruleId);
            return activity;
        }
    }

    public class DependencyAwareValidator<T> : IValidator<T>
    {
        private readonly ExecutionPlan<T> _executionPlan;
        private readonly ILogger<DependencyAwareValidator<T>> _logger;
        private readonly DependencyAwareValidatorOptions<T> _options;

        public DependencyAwareValidator(
            IEnumerable<IValidationRule<T>> rules,
            ILogger<DependencyAwareValidator<T>>? logger = null,
            DependencyAwareValidatorOptions<T>? options = null)
        {
            var ruleMap = rules.ToDictionary(r => r.Id);
            _executionPlan = ExecutionPlan<T>.CreateDependencyAware(ruleMap.Values);
            _logger = logger ?? NullLogger<DependencyAwareValidator<T>>.Instance;
            _options = options ?? new DependencyAwareValidatorOptions<T>();
        }

        public ValidationResult CollectValidationResults(T input)
        {
            using var scope = ValidationContextScope.Begin();
            _logger.LogInformation("Starting validation of {InputType}", typeof(T).Name);
            var final = new ValidationResult();
            var context = ValidationContext.Instance;
            var ruleOutcomes = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (var stage in _executionPlan.Stages)
            {
                if (_options.EnableParallelStages && stage.Count > 1)
                {
                    ExecuteStageParallel(input, stage, final, context, ruleOutcomes);
                    continue;
                }

                foreach (var step in stage)
                {
                    var outcome = ExecuteStep(input, step, ruleOutcomes);
                    PersistStepOutcome(step, outcome, context, ruleOutcomes, final);
                }
            }

            _logger.LogInformation("Finished validation of {InputType}", typeof(T).Name);
            return final;
        }

        private void ExecuteStageParallel(
            T input,
            IReadOnlyList<ExecutionPlanStep<T>> stage,
            ValidationResult final,
            ValidationContext context,
            Dictionary<string, bool> ruleOutcomes)
        {
            var stageOutcomes = new StepOutcome[stage.Count];
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, _options.MaxDegreeOfParallelism)
            };

            Parallel.ForEach(
                Enumerable.Range(0, stage.Count),
                parallelOptions,
                index => { stageOutcomes[index] = ExecuteStep(input, stage[index], ruleOutcomes); });

            for (int i = 0; i < stage.Count; i++)
            {
                PersistStepOutcome(stage[i], stageOutcomes[i], context, ruleOutcomes, final);
            }
        }

        private StepOutcome ExecuteStep(T input, ExecutionPlanStep<T> step, IReadOnlyDictionary<string, bool> outcomes)
        {
            if (!ShouldExecute(step, outcomes))
            {
                _options.Hooks?.OnRuleSkipped?.Invoke(step.RuleId);
                ValidationTelemetry.RuleSkips.Add(1, KeyValuePair.Create<string, object?>("rule.id", step.RuleId));
                _logger.LogDebug("Rule {RuleId} skipped due to dependency gate.", step.RuleId);
                return StepOutcome.Skipped();
            }

            _options.Hooks?.OnRuleStart?.Invoke(step.RuleId);
            var startedAt = Stopwatch.GetTimestamp();
            using var activity = ValidationTelemetry.StartRuleActivity(step.RuleId);

            try
            {
                step.Execute(input);
                _options.Hooks?.OnRuleSuccess?.Invoke(step.RuleId);
                return StepOutcome.Success(startedAt);
            }
            catch (Exception ex)
            {
                _options.Hooks?.OnRuleFailure?.Invoke(step.RuleId, ex);
                ValidationTelemetry.RuleFailures.Add(1, KeyValuePair.Create<string, object?>("rule.id", step.RuleId));
                return StepOutcome.Failure(ex, startedAt);
            }
        }

        private void PersistStepOutcome(
            ExecutionPlanStep<T> step,
            StepOutcome outcome,
            ValidationContext context,
            IDictionary<string, bool> ruleOutcomes,
            ValidationResult final)
        {
            context.RuleResults[step.RuleId] = new RuleExecutionResult { Success = outcome.IsSuccess };
            ruleOutcomes[step.RuleId] = outcome.IsSuccess;

            if (outcome.DurationMs.HasValue)
            {
                ValidationTelemetry.RuleDurationMs.Record(
                    outcome.DurationMs.Value,
                    KeyValuePair.Create<string, object?>("rule.id", step.RuleId),
                    KeyValuePair.Create<string, object?>("outcome", outcome.IsSuccess ? "success" : "failure"));
            }

            if (outcome.Exception == null)
            {
                if (outcome.IsSkipped)
                {
                    return;
                }

                _logger.LogDebug("Rule {RuleId} executed successfully", step.RuleId);
                return;
            }

            final.AddError(outcome.Exception.Message, step.Severity, code: step.RuleId, path: step.RuleId);
            _logger.LogError(outcome.Exception, "Rule {RuleId} failed: {Message}", step.RuleId, outcome.Exception.Message);
        }

        private static bool ShouldExecute(ExecutionPlanStep<T> step, IReadOnlyDictionary<string, bool> outcomes)
        {
            if (step.Dependencies.Count == 0 || step.DependencyType == null)
            {
                return true;
            }

            var dependencyOutcomes = step.Dependencies.Select(id => outcomes.TryGetValue(id, out var ok) && ok).ToArray();
            return step.DependencyType.Value switch
            {
                DependencyType.RequiresAllSuccess => dependencyOutcomes.All(x => x),
                DependencyType.RequiresAnyFailure => dependencyOutcomes.Any(x => !x),
                DependencyType.RequiresAnySuccess => dependencyOutcomes.Any(x => x),
                _ => true
            };
        }

        public bool IsValid(T input) => CollectValidationResults(input).IsValid;
        public ValidationError? GetFirstError(T input) => CollectValidationResults(input).Errors.FirstOrDefault();
        public void ValidateOrThrow(T input) => CollectValidationResults(input).ThrowIfInvalid();
        public ValidationResult ValidateAndProcess(T input, Action<T> action)
        {
            var res = CollectValidationResults(input);
            if (res.IsValid) action(input);
            return res;
        }
        public void ValidateAndExecute(T input, Action successAction, Action<IReadOnlyList<ValidationError>> failureAction)
        {
            var res = CollectValidationResults(input);
            if (res.IsValid) successAction(); else failureAction(res.Errors);
        }

        private readonly record struct StepOutcome(bool IsSuccess, bool IsSkipped, Exception? Exception, double? DurationMs)
        {
            public static StepOutcome Success(long startedAt) =>
                new(true, false, null, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            public static StepOutcome Failure(Exception exception, long startedAt) =>
                new(false, false, exception, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            public static StepOutcome Skipped() =>
                new(true, true, null, null);
        }
    }
}
