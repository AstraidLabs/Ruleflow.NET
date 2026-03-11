using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ruleflow.NET.Engine.Validation;
using Ruleflow.NET.Engine.Validation.Core.Results;
using Ruleflow.NET.Engine.Validation.Core.Validators;
using Ruleflow.NET.Engine.Validation.Enums;
using Ruleflow.NET.Engine.Validation.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ruleflow.NET.Tests
{
    [TestClass]
    public class ExecutionPlanAndAsyncValidatorTests
    {
        [TestMethod]
        public void Validator_UsesCompiledExecutionPlan_WhenRulePriorityChangesAfterConstruction()
        {
            var executionOrder = new List<string>();
            var first = RuleflowExtensions.CreateRule<string>()
                .WithId("first")
                .WithPriority(10)
                .WithAction(_ => executionOrder.Add("first"))
                .Build();

            var second = RuleflowExtensions.CreateRule<string>()
                .WithId("second")
                .WithPriority(1)
                .WithAction(_ => executionOrder.Add("second"))
                .Build();

            var validator = new Validator<string>(new[] { first, second });

            first.SetPriority(-100);
            second.SetPriority(200);

            validator.CollectValidationResults("ok");

            CollectionAssert.AreEqual(new[] { "first", "second" }, executionOrder);
        }

        [TestMethod]
        public async Task AsyncValidator_AdaptsSyncRules_AndPropagatesCancellationToken()
        {
            var syncRule = RuleflowExtensions.CreateRule<string>()
                .WithId("sync")
                .WithAction(input =>
                {
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        throw new ArgumentException("Input is required");
                    }
                })
                .Build();

            var asyncRule = RuleflowExtensions.CreateAsyncRule<string>()
                .WithId("async")
                .WithAction(async (_, ct) =>
                {
                    await Task.Delay(10, ct);
                    ct.ThrowIfCancellationRequested();
                })
                .Build();

            var allRules = new IAsyncValidationRule<string>[]
            {
                new Ruleflow.NET.Engine.Validation.Core.Base.SyncToAsyncValidationRuleAdapter<string>(syncRule),
                asyncRule
            };

            var validator = new AsyncValidator<string>(allRules);
            using var cts = new CancellationTokenSource();

            var result = await validator.CollectValidationResultsAsync("value", cts.Token);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        public void ValidationResult_FormatErrors_UsesFormatter()
        {
            var result = new ValidationResult();
            result.AddError("Email invalid", ValidationSeverity.Error, code: "VAL-1", path: "Order.Customer.Email");

            var formatted = result.FormatErrors(new TestErrorFormatter());

            Assert.AreEqual(1, formatted.Count);
            Assert.AreEqual("VAL-1:Order.Customer.Email", formatted.Single());
        }

        private sealed class TestErrorFormatter : IErrorFormatter
        {
            public object Format(ValidationError error) => $"{error.Code}:{error.Path}";
        }
    }
}
