using System;
using System.Collections.Generic;
using System.Linq;
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
    public class DependencyAwareValidator<T> : IValidator<T>
    {
        private readonly ExecutionPlan<T> _executionPlan;
        private readonly ILogger<DependencyAwareValidator<T>> _logger;

        public DependencyAwareValidator(IEnumerable<IValidationRule<T>> rules, ILogger<DependencyAwareValidator<T>>? logger = null)
        {
            var ruleMap = rules.ToDictionary(r => r.Id);
            _executionPlan = ExecutionPlan<T>.CreateDependencyAware(ruleMap.Values);
            _logger = logger ?? NullLogger<DependencyAwareValidator<T>>.Instance;
        }

        public ValidationResult CollectValidationResults(T input)
        {
            _logger.LogInformation("Starting validation of {InputType}", typeof(T).Name);
            var final = new ValidationResult();
            var context = ValidationContext.Instance;
            var ruleOutcomes = new Dictionary<string, bool>();
            foreach (var step in _executionPlan.Steps)
            {
                if (!ShouldExecute(step, ruleOutcomes))
                {
                    context.RuleResults[step.RuleId] = new RuleExecutionResult { Success = true };
                    ruleOutcomes[step.RuleId] = true;
                    continue;
                }

                try
                {
                    step.Execute(input);
                    context.RuleResults[step.RuleId] = new RuleExecutionResult { Success = true };
                    ruleOutcomes[step.RuleId] = true;
                    _logger.LogDebug("Rule {RuleId} executed successfully", step.RuleId);
                }
                catch (Exception ex)
                {
                    context.RuleResults[step.RuleId] = new RuleExecutionResult { Success = false };
                    ruleOutcomes[step.RuleId] = false;
                    final.AddError(ex.Message, step.Severity, code: step.RuleId, path: step.RuleId);
                    _logger.LogError(ex, "Rule {RuleId} failed: {Message}", step.RuleId, ex.Message);
                }
            }
            _logger.LogInformation("Finished validation of {InputType}", typeof(T).Name);
            return final;
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
    }
}
