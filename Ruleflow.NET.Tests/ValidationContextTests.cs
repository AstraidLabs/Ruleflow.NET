using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ruleflow.NET.Engine.Validation;
using Ruleflow.NET.Engine.Validation.Core.Base;
using Ruleflow.NET.Engine.Validation.Core.Context;
using Ruleflow.NET.Engine.Validation.Core.Results;
using Ruleflow.NET.Engine.Validation.Core.Validators;
using Ruleflow.NET.Engine.Validation.Enums;
using Ruleflow.NET.Engine.Validation.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ruleflow.NET.Tests
{
    [TestClass]
    public class ValidationContextTests
    {
        private class ShoppingCart
        {
            public int Id { get; set; }
            public List<CartItem> Items { get; set; } = new List<CartItem>();
            public string PromoCode { get; set; }
            public decimal TotalAmount { get; set; }
            public string CustomerId { get; set; }
            public bool HasShippingAddress { get; set; }
        }

        private class CartItem
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public decimal Price { get; set; }
            public int Quantity { get; set; }
        }

        [TestInitialize]
        public void SetUp()
        {
            // Ensure scoped mode for tests; the global fallback is cleared
            // so legacy tests that read Instance outside a scope still work.
            ValidationContext.Mode = ValidationContextMode.ScopedAsyncFlow;
            ValidationContext.Instance.Clear();
        }

        [TestMethod]
        public void ValidationContext_PropagatesProperty_BetweenRules()
        {
            // Arrange
            var calculateTotalRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                .WithId("CalculateTotalRule")
                .WithAction(cart => {
                    decimal calculatedTotal = 0;
                    foreach (var item in cart.Items)
                    {
                        calculatedTotal += item.Price * item.Quantity;
                    }
                    ValidationContext.Instance.Properties["CalculatedTotal"] = calculatedTotal;
                    if (Math.Abs(calculatedTotal - cart.TotalAmount) > 0.01m)
                    {
                        throw new ArgumentException($"Cart total mismatch. Expected: {calculatedTotal}, Actual: {cart.TotalAmount}");
                    }
                })
                .Build();

            var contextAccessRule = new ContextAwareTestRule("ContextAccessRule");
            var rules = new List<IValidationRule<ShoppingCart>> { calculateTotalRule, contextAccessRule };
            var validator = new DependencyAwareValidator<ShoppingCart>(rules);

            var cart = new ShoppingCart
            {
                Id = 1,
                Items = new List<CartItem>
                {
                    new CartItem { ProductId = 101, ProductName = "Product 1", Price = 10.99m, Quantity = 2 },
                    new CartItem { ProductId = 102, ProductName = "Product 2", Price = 24.99m, Quantity = 1 }
                },
                TotalAmount = 46.97m
            };

            // Act
            var result = validator.CollectValidationResults(cart);

            // Assert
            Assert.IsTrue(result.IsValid, "Validation failed unexpectedly");
            Assert.IsTrue(contextAccessRule.ContextAccessSuccessful, "Context property was not accessible");
        }

        [TestMethod]
        public void ValidationContext_PropagatesRuleResults_ToOtherRules()
        {
            // Arrange
            var hasItemsRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                .WithId("HasItemsRule")
                .WithAction(cart => {
                    if (cart.Items.Count == 0)
                        throw new ArgumentException("Cart has no items");
                })
                .Build();

            var promoCodeRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                .WithId("PromoCodeRule")
                .WithAction(cart => {
                    if (string.IsNullOrEmpty(cart.PromoCode))
                        return;
                    if (cart.PromoCode != "VALID10" && cart.PromoCode != "VALID20")
                        throw new ArgumentException("Invalid promo code");
                })
                .Build();

            var resultCheckRule = new RuleResultCheckingRule("ResultCheckRule",
                new[] { "HasItemsRule", "PromoCodeRule" });

            var rules = new List<IValidationRule<ShoppingCart>> { hasItemsRule, promoCodeRule, resultCheckRule };
            var validator = new DependencyAwareValidator<ShoppingCart>(rules);

            var validCart = new ShoppingCart
            {
                Id = 1,
                Items = new List<CartItem> { new CartItem { ProductId = 101, ProductName = "Product 1", Price = 10.99m, Quantity = 1 } },
                PromoCode = "VALID10"
            };

            // Act
            var result = validator.CollectValidationResults(validCart);

            // Assert
            Assert.IsTrue(result.IsValid, "Validation failed unexpectedly");
            Assert.IsTrue(resultCheckRule.AllRulesSucceeded, "Rule results were not accessible in context");
        }

        [TestMethod]
        public void ValidationContext_AllowsRules_ToCheckFailedDependencies()
        {
            // Arrange
            var cartValidRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                .WithId("CartValidRule")
                .WithAction(cart => {
                    throw new ArgumentException("Cart validation failed");
                })
                .Build();

            var failureCheckRule = new RuleFailureCheckingRule("FailureCheckRule", "CartValidRule");
            var rules = new List<IValidationRule<ShoppingCart>> { cartValidRule, failureCheckRule };
            var validator = new DependencyAwareValidator<ShoppingCart>(rules);

            var cart = new ShoppingCart { Id = 1 };

            // Act
            var result = validator.CollectValidationResults(cart);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count, "Expected exactly one error");
            Assert.AreEqual("Cart validation failed", result.Errors[0].Message);
            Assert.IsTrue(failureCheckRule.FailureDetected, "Rule failure was not accessible in context");
        }

        [TestMethod]
        public void ValidationContext_CanStoreAndRetrieveCustomProperties()
        {
            var context = new ValidationContext();

            context.Properties["StringValue"] = "Test String";
            context.Properties["IntValue"] = 42;
            context.Properties["DateValue"] = new DateTime(2025, 4, 24);
            context.Properties["ListValue"] = new List<string> { "One", "Two", "Three" };
            context.Properties["ObjectValue"] = new CartItem { ProductId = 101, Price = 19.99m };

            Assert.AreEqual("Test String", context.Properties["StringValue"]);
            Assert.AreEqual(42, context.Properties["IntValue"]);
            Assert.AreEqual(new DateTime(2025, 4, 24), context.Properties["DateValue"]);

            var retrievedList = context.Properties["ListValue"] as List<string>;
            Assert.IsNotNull(retrievedList, "Failed to retrieve list");
            Assert.AreEqual(3, retrievedList.Count);
            Assert.AreEqual("Two", retrievedList[1]);

            var retrievedObject = context.Properties["ObjectValue"] as CartItem;
            Assert.IsNotNull(retrievedObject, "Failed to retrieve object");
            Assert.AreEqual(101, retrievedObject.ProductId);
            Assert.AreEqual(19.99m, retrievedObject.Price);
        }

        [TestMethod]
        public void ValidationContext_HandlesNonExistentProperties_Gracefully()
        {
            var context = new ValidationContext();
            context.Properties["ExistingKey"] = "Value";

            Assert.ThrowsException<KeyNotFoundException>(() => {
                var value = context.Properties["NonExistentKey"];
            });

            Assert.AreEqual("Value", context.Properties["ExistingKey"]);
        }

        [TestMethod]
        public void ValidationContext_GetErrorsBySeverity_FiltersCorrectedlyByLevel()
        {
            var verboseRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                .WithId("VerboseRule")
                .WithSeverity(ValidationSeverity.Verbose)
                .WithAction(cart => { throw new ArgumentException("Verbose level message"); })
                .Build();

            var infoRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                .WithId("InfoRule")
                .WithSeverity(ValidationSeverity.Information)
                .WithAction(cart => { throw new ArgumentException("Information level message"); })
                .Build();

            var warningRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                .WithId("WarningRule")
                .WithSeverity(ValidationSeverity.Warning)
                .WithAction(cart => { throw new ArgumentException("Warning level message"); })
                .Build();

            var errorRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                .WithId("ErrorRule")
                .WithSeverity(ValidationSeverity.Error)
                .WithAction(cart => { throw new ArgumentException("Error level message"); })
                .Build();

            var criticalRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                .WithId("CriticalRule")
                .WithSeverity(ValidationSeverity.Critical)
                .WithAction(cart => { throw new ArgumentException("Critical level message"); })
                .Build();

            var validator = new DependencyAwareValidator<ShoppingCart>(
                new[] { verboseRule, infoRule, warningRule, errorRule, criticalRule });

            var cart = new ShoppingCart { Id = 1 };

            var result = validator.CollectValidationResults(cart);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(5, result.Errors.Count);

            Assert.AreEqual(1, result.GetErrorsBySeverity(ValidationSeverity.Verbose).Count());
            Assert.AreEqual(1, result.GetErrorsBySeverity(ValidationSeverity.Information).Count());
            Assert.AreEqual(1, result.GetErrorsBySeverity(ValidationSeverity.Warning).Count());
            Assert.AreEqual(1, result.GetErrorsBySeverity(ValidationSeverity.Error).Count());
            Assert.AreEqual(1, result.GetErrorsBySeverity(ValidationSeverity.Critical).Count());

            Assert.AreEqual("Verbose level message", result.GetErrorsBySeverity(ValidationSeverity.Verbose).First().Message);
            Assert.AreEqual("Information level message", result.GetErrorsBySeverity(ValidationSeverity.Information).First().Message);
            Assert.AreEqual("Warning level message", result.GetErrorsBySeverity(ValidationSeverity.Warning).First().Message);
            Assert.AreEqual("Error level message", result.GetErrorsBySeverity(ValidationSeverity.Error).First().Message);
            Assert.AreEqual("Critical level message", result.GetErrorsBySeverity(ValidationSeverity.Critical).First().Message);
        }

        [TestMethod]
        public async Task ValidationContext_Isolated_BetweenConcurrentRuns()
        {
            // This test verifies that concurrent validation runs do not interfere with each other.
            // With the old global singleton, concurrent runs would overwrite each other's RunId,
            // causing mismatches. With scoped contexts this works correctly.
            ValidationContext.Mode = ValidationContextMode.ScopedAsyncFlow;

            const int concurrentRuns = 100;
            var tasks = new Task<ValidationResult>[concurrentRuns];

            for (int i = 0; i < concurrentRuns; i++)
            {
                var runId = $"run-{i}";
                tasks[i] = Task.Run(() =>
                {
                    var storeRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                        .WithId("StoreRunId")
                        .WithAction(cart =>
                        {
                            ValidationContext.Instance.Properties["RunId"] = runId;
                        })
                        .Build();

                    var checkRule = RuleflowExtensions.CreateRule<ShoppingCart>()
                        .WithId("CheckRunId")
                        .WithAction(cart =>
                        {
                            if (!ValidationContext.Instance.Properties.TryGetValue("RunId", out var stored)
                                || (string)stored! != runId)
                            {
                                throw new InvalidOperationException(
                                    $"RunId mismatch: expected '{runId}', got '{stored}'");
                            }
                        })
                        .Build();

                    var validator = new Validator<ShoppingCart>(new[] { storeRule, checkRule });
                    return validator.CollectValidationResults(new ShoppingCart { Id = 1 });
                });
            }

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                Assert.IsTrue(result.IsValid,
                    "Concurrent validation run failed — context isolation is broken. Errors: "
                    + string.Join("; ", result.Errors.Select(e => e.Message)));
            }
        }

        // Helper classes

        private class ContextAwareTestRule : IdentifiableValidationRule<ShoppingCart>
        {
            public bool ContextAccessSuccessful { get; private set; }

            public ContextAwareTestRule(string id) : base(id) { }

            public override ValidationSeverity DefaultSeverity => ValidationSeverity.Error;

            public override void Validate(ShoppingCart input)
            {
                var context = ValidationContext.Instance;
                if (context.Properties.TryGetValue("CalculatedTotal", out var calculatedTotal))
                {
                    decimal total = (decimal)calculatedTotal!;
                    ContextAccessSuccessful = Math.Abs(total - input.TotalAmount) <= 0.01m;
                }
                else
                {
                    ContextAccessSuccessful = false;
                }

                if (!ContextAccessSuccessful)
                {
                    throw new ArgumentException("Context property 'CalculatedTotal' was not accessible");
                }
            }
        }

        private class RuleResultCheckingRule : IdentifiableValidationRule<ShoppingCart>
        {
            private readonly string[] _rulesToCheck;
            public bool AllRulesSucceeded { get; private set; }

            public RuleResultCheckingRule(string id, string[] rulesToCheck) : base(id)
            {
                _rulesToCheck = rulesToCheck;
            }

            public override ValidationSeverity DefaultSeverity => ValidationSeverity.Error;

            public override void Validate(ShoppingCart input)
            {
                var context = ValidationContext.Instance;

                AllRulesSucceeded = true;
                foreach (var ruleId in _rulesToCheck)
                {
                    if (!context.RuleResults.TryGetValue(ruleId, out var result) || !result.Success)
                    {
                        AllRulesSucceeded = false;
                        throw new ArgumentException($"Rule {ruleId} did not succeed");
                    }
                }
            }
        }

        private class RuleFailureCheckingRule : IdentifiableValidationRule<ShoppingCart>
        {
            private readonly string _ruleToCheck;
            public bool FailureDetected { get; private set; }

            public RuleFailureCheckingRule(string id, string ruleToCheck) : base(id)
            {
                _ruleToCheck = ruleToCheck;
            }

            public override ValidationSeverity DefaultSeverity => ValidationSeverity.Error;

            public override void Validate(ShoppingCart input)
            {
                var context = ValidationContext.Instance;

                if (context.RuleResults.TryGetValue(_ruleToCheck, out var result) && !result.Success)
                {
                    FailureDetected = true;
                }
                else
                {
                    FailureDetected = false;
                    throw new ArgumentException($"Failed to detect failure of rule {_ruleToCheck}");
                }
            }
        }
    }
}
