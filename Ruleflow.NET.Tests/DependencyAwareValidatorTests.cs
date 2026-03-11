using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ruleflow.NET.Engine.Validation;
using Ruleflow.NET.Engine.Validation.Core.Validators;
using Ruleflow.NET.Engine.Validation.Enums;
using Ruleflow.NET.Engine.Validation.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace Ruleflow.NET.Tests
{
    [TestClass]
    public class DependencyAwareValidatorTests
    {
        private class UserProfile
        {
            public int Id { get; set; }
            public string Username { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public bool IsEmailVerified { get; set; }
            public bool IsPremium { get; set; }
            public List<string> Roles { get; set; } = new List<string>();
        }

        [TestMethod]
        public void DependentRule_ExecutesAfterDependency_WhenRequiresAllSuccess()
        {
            // Arrange
            // First rule validates the username
            var usernameRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("UsernameValidation")
                .WithAction(p => {
                    if (string.IsNullOrEmpty(p.Username))
                        throw new ArgumentException("Username is required");
                    if (p.Username.Length < 3)
                        throw new ArgumentException("Username must be at least 3 characters");
                })
                .Build();

            // Second rule depends on the username rule and validates email format
            var emailRule = RuleflowExtensions.CreateDependentRule<UserProfile>("EmailValidation")
                .DependsOn("UsernameValidation")
                .WithDependencyType(DependencyType.RequiresAllSuccess)
                .WithAction(p => {
                    if (string.IsNullOrEmpty(p.Email))
                        throw new ArgumentException("Email is required");
                    if (!p.Email.Contains("@"))
                        throw new ArgumentException("Email must contain @");
                })
                .Build();

            var validator = new DependencyAwareValidator<UserProfile>(new[] { usernameRule, emailRule });

            // Act - With valid username but invalid email
            var userProfile = new UserProfile
            {
                Id = 1,
                Username = "john",
                Email = "invalid-email"  // Missing @
            };

            var result = validator.CollectValidationResults(userProfile);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.AreEqual("Email must contain @", result.Errors[0].Message);
        }

        [TestMethod]
        public void DependentRule_NotExecuted_WhenDependencyFails_AndRequiresAllSuccess()
        {
            // Arrange
            // First rule validates the username
            var usernameRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("UsernameValidation")
                .WithAction(p => {
                    if (string.IsNullOrEmpty(p.Username))
                        throw new ArgumentException("Username is required");
                    if (p.Username.Length < 3)
                        throw new ArgumentException("Username must be at least 3 characters");
                })
                .Build();

            // Second rule depends on the username rule and validates email format
            var emailRule = RuleflowExtensions.CreateDependentRule<UserProfile>("EmailValidation")
                .DependsOn("UsernameValidation")
                .WithDependencyType(DependencyType.RequiresAllSuccess)
                .WithAction(p => {
                    if (string.IsNullOrEmpty(p.Email))
                        throw new ArgumentException("Email is required");
                    if (!p.Email.Contains("@"))
                        throw new ArgumentException("Email must contain @");
                })
                .Build();

            var validator = new DependencyAwareValidator<UserProfile>(new[] { usernameRule, emailRule });

            // Act - With invalid username and invalid email
            var userProfile = new UserProfile
            {
                Id = 1,
                Username = "jo",  // Too short
                Email = "invalid-email"  // Missing @
            };

            var result = validator.CollectValidationResults(userProfile);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.AreEqual("Username must be at least 3 characters", result.Errors[0].Message);
            // Email rule should not execute since username rule failed
        }

        [TestMethod]
        public void DependentRule_ExecutesWhenDependencyFails_AndRequiresAnyFailure()
        {
            // Arrange
            // First rule checks if user is premium
            var premiumRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("PremiumValidation")
                .WithAction(p => {
                    if (!p.IsPremium)
                        throw new ArgumentException("User is not a premium user");
                })
                .Build();

            // Second rule executes only when premium check fails
            var roleRule = RuleflowExtensions.CreateDependentRule<UserProfile>("RoleValidation")
                .DependsOn("PremiumValidation")
                .WithDependencyType(DependencyType.RequiresAnyFailure)
                .WithAction(p => {
                    if (!p.Roles.Contains("basic-access"))
                        throw new ArgumentException("Non-premium users must have the basic-access role");
                })
                .Build();

            var validator = new DependencyAwareValidator<UserProfile>(new[] { premiumRule, roleRule });

            // Act - With non-premium user missing the required role
            var userProfile = new UserProfile
            {
                Id = 1,
                Username = "john",
                Email = "john@example.com",
                IsPremium = false,
                Roles = new List<string>()  // Empty roles
            };

            var result = validator.CollectValidationResults(userProfile);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(2, result.Errors.Count);

            bool premiumErrorFound = false;
            bool roleErrorFound = false;

            foreach (var error in result.Errors)
            {
                if (error.Message == "User is not a premium user")
                    premiumErrorFound = true;
                if (error.Message == "Non-premium users must have the basic-access role")
                    roleErrorFound = true;
            }

            Assert.IsTrue(premiumErrorFound, "Premium validation error not found");
            Assert.IsTrue(roleErrorFound, "Role validation error not found");
        }

        [TestMethod]
        public void DependentRule_NotExecuted_WhenDependencySucceeds_AndRequiresAnyFailure()
        {
            // Arrange
            // First rule checks if user is premium
            var premiumRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("PremiumValidation")
                .WithAction(p => {
                    if (!p.IsPremium)
                        throw new ArgumentException("User is not a premium user");
                })
                .Build();

            // Second rule executes only when premium check fails
            var roleRule = RuleflowExtensions.CreateDependentRule<UserProfile>("RoleValidation")
                .DependsOn("PremiumValidation")
                .WithDependencyType(DependencyType.RequiresAnyFailure)
                .WithAction(p => {
                    if (!p.Roles.Contains("basic-access"))
                        throw new ArgumentException("Non-premium users must have the basic-access role");
                })
                .Build();

            var validator = new DependencyAwareValidator<UserProfile>(new[] { premiumRule, roleRule });

            // Act - With premium user (first rule succeeds)
            var userProfile = new UserProfile
            {
                Id = 1,
                Username = "john",
                Email = "john@example.com",
                IsPremium = true,
                Roles = new List<string>()  // Empty roles - would fail if rule executed
            };

            var result = validator.CollectValidationResults(userProfile);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
            // Second rule should not execute since it requires the first rule to fail
        }

        [TestMethod]
        public void DependentRule_WithMultipleDependencies_RequiresAllSuccess()
        {
            // Arrange
            // Username validation rule
            var usernameRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("UsernameValidation")
                .WithAction(p => {
                    if (string.IsNullOrEmpty(p.Username))
                        throw new ArgumentException("Username is required");
                })
                .Build();

            // Email validation rule
            var emailRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("EmailValidation")
                .WithAction(p => {
                    if (string.IsNullOrEmpty(p.Email))
                        throw new ArgumentException("Email is required");
                })
                .Build();

            // Password rule depends on both username and email rules
            var passwordRule = RuleflowExtensions.CreateDependentRule<UserProfile>("PasswordValidation")
                .DependsOn("UsernameValidation", "EmailValidation")
                .WithDependencyType(DependencyType.RequiresAllSuccess)
                .WithAction(p => {
                    if (string.IsNullOrEmpty(p.Password))
                        throw new ArgumentException("Password is required");
                    if (p.Password.Length < 8)
                        throw new ArgumentException("Password must be at least 8 characters");
                })
                .Build();

            var validator = new DependencyAwareValidator<UserProfile>(
                new[] { usernameRule, emailRule, passwordRule });

            // Act - With all fields valid
            var validProfile = new UserProfile
            {
                Id = 1,
                Username = "john",
                Email = "john@example.com",
                Password = "password123"
            };

            var validResult = validator.CollectValidationResults(validProfile);

            // Assert
            Assert.IsTrue(validResult.IsValid);
            Assert.AreEqual(0, validResult.Errors.Count);

            // Act - With missing email (one dependency fails)
            var invalidProfile = new UserProfile
            {
                Id = 2,
                Username = "jane",
                Email = "",  // Missing email
                Password = "password123"
            };

            var invalidResult = validator.CollectValidationResults(invalidProfile);

            // Assert
            Assert.IsFalse(invalidResult.IsValid);
            Assert.AreEqual(1, invalidResult.Errors.Count);
            Assert.AreEqual("Email is required", invalidResult.Errors[0].Message);
            // Password rule shouldn't execute because not all dependencies succeed
        }

        [TestMethod]
        public void DependentRule_WithMultipleDependencies_RequiresAnySuccess()
        {
            // Arrange
            // Username validation rule
            var usernameRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("UsernameValidation")
                .WithAction(p => {
                    if (string.IsNullOrEmpty(p.Username))
                        throw new ArgumentException("Username is required");
                })
                .Build();

            // Email validation rule
            var emailRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("EmailValidation")
                .WithAction(p => {
                    if (string.IsNullOrEmpty(p.Email))
                        throw new ArgumentException("Email is required");
                })
                .Build();

            // Verification rule depends on either username or email being valid
            var verificationRule = RuleflowExtensions.CreateDependentRule<UserProfile>("VerificationRule")
                .DependsOn("UsernameValidation", "EmailValidation")
                .WithDependencyType(DependencyType.RequiresAnySuccess)
                .WithAction(p => {
                    if (!p.IsEmailVerified)
                        throw new ArgumentException("Email must be verified");
                })
                .Build();

            var validator = new DependencyAwareValidator<UserProfile>(
                new[] { usernameRule, emailRule, verificationRule });

            // Act - With valid username but invalid email
            var profile = new UserProfile
            {
                Id = 1,
                Username = "john",
                Email = "",  // Invalid email
                IsEmailVerified = false
            };

            var result = validator.CollectValidationResults(profile);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(2, result.Errors.Count);

            bool emailErrorFound = false;
            bool verificationErrorFound = false;

            foreach (var error in result.Errors)
            {
                if (error.Message == "Email is required")
                    emailErrorFound = true;
                if (error.Message == "Email must be verified")
                    verificationErrorFound = true;
            }

            Assert.IsTrue(emailErrorFound, "Email validation error not found");
            Assert.IsTrue(verificationErrorFound, "Verification error not found");
            // Verification rule should execute because username validation succeeded
        }

        [TestMethod]
        public void DependentRule_WithCircularDependency_ThrowsException()
        {
            // Arrange
            // Rule A depends on Rule B
            var ruleA = RuleflowExtensions.CreateDependentRule<UserProfile>("RuleA")
                .DependsOn("RuleB")
                .WithDependencyType(DependencyType.RequiresAllSuccess)
                .WithAction(p => { /* No action needed for test */ })
                .Build();

            // Rule B depends on Rule A - creating a circular dependency
            var ruleB = RuleflowExtensions.CreateDependentRule<UserProfile>("RuleB")
                .DependsOn("RuleA")
                .WithDependencyType(DependencyType.RequiresAllSuccess)
                .WithAction(p => { /* No action needed for test */ })
                .Build();

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() =>
                new DependencyAwareValidator<UserProfile>(new[] { ruleA, ruleB }));
        }


        [TestMethod]
        public void DependencyAwareValidator_ParallelStages_ExecutesIndependentRulesConcurrentlyAndDeterministically()
        {
            var executionOrder = new List<string>();
            var gate = new ManualResetEventSlim(false);

            var slowRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("RuleB")
                .WithPriority(10)
                .WithAction(_ =>
                {
                    gate.Wait();
                    lock (executionOrder) executionOrder.Add("RuleB");
                })
                .Build();

            var fastRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("RuleA")
                .WithPriority(10)
                .WithAction(_ =>
                {
                    lock (executionOrder) executionOrder.Add("RuleA");
                    gate.Set();
                })
                .Build();

            var dependentRule = RuleflowExtensions.CreateDependentRule<UserProfile>("RuleC")
                .DependsOn("RuleA", "RuleB")
                .WithDependencyType(DependencyType.RequiresAllSuccess)
                .WithAction(_ =>
                {
                    lock (executionOrder) executionOrder.Add("RuleC");
                })
                .Build();

            var validator = new DependencyAwareValidator<UserProfile>(
                new[] { slowRule, fastRule, dependentRule },
                options: new DependencyAwareValidatorOptions<UserProfile>
                {
                    EnableParallelStages = true,
                    MaxDegreeOfParallelism = 2
                });

            var result = validator.CollectValidationResults(new UserProfile { Username = "john", Email = "john@example.com" });

            Assert.IsTrue(result.IsValid);
            CollectionAssert.AreEqual(new[] { "RuleA", "RuleB", "RuleC" }, executionOrder);
        }

        [TestMethod]
        public void DependencyAwareValidator_Hooks_AreInvokedForStartSuccessFailureAndSkip()
        {
            var events = new ConcurrentQueue<string>();

            var successRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("SuccessRule")
                .WithAction(_ => { })
                .Build();

            var failedRule = RuleflowExtensions.CreateRule<UserProfile>()
                .WithId("FailedRule")
                .WithAction(_ => throw new InvalidOperationException("boom"))
                .Build();

            var skippedRule = RuleflowExtensions.CreateDependentRule<UserProfile>("SkippedRule")
                .DependsOn("SuccessRule")
                .WithDependencyType(DependencyType.RequiresAnyFailure)
                .WithAction(_ => throw new InvalidOperationException("should not run"))
                .Build();

            var validator = new DependencyAwareValidator<UserProfile>(
                new[] { successRule, failedRule, skippedRule },
                options: new DependencyAwareValidatorOptions<UserProfile>
                {
                    Hooks = new RuleExecutionHooks<UserProfile>
                    {
                        OnRuleStart = id => events.Enqueue($"start:{id}"),
                        OnRuleSuccess = id => events.Enqueue($"success:{id}"),
                        OnRuleFailure = (id, _) => events.Enqueue($"failure:{id}"),
                        OnRuleSkipped = id => events.Enqueue($"skipped:{id}")
                    }
                });

            var result = validator.CollectValidationResults(new UserProfile { Username = "john", Email = "john@example.com" });

            Assert.IsFalse(result.IsValid);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "start:SuccessRule", "success:SuccessRule",
                    "start:FailedRule", "failure:FailedRule",
                    "skipped:SkippedRule"
                },
                events.ToArray());
        }

    }
}