using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ruleflow.NET.Engine.Validation;
using Ruleflow.NET.Engine.Validation.Enums;
using Ruleflow.NET.Extensions;
using Ruleflow.NET.Engine.Registry.Interface;
using Ruleflow.NET.Engine.Validation.Interfaces;
using Ruleflow.NET.Engine.Validation.Core.Context;
using Ruleflow.NET.Engine.Data.Enums;
using Ruleflow.NET.Engine.Data.Mapping;
using Ruleflow.NET.Engine.Events;
using Ruleflow.NET.Engine.Cqrs.Commands;
using Ruleflow.NET.Engine.Cqrs.Queries;

namespace Ruleflow.NET.Tests
{
    // Attribute rule used by the AutoRegisterAttributeRules test.
    // Must be a top-level (non-nested) class so the attribute scanner can find it.
    public static class PersonAttributeRules
    {
        [ValidationRule("age.nonnegative", Priority = 1)]
        public static void NonNegative(ServiceCollectionExtensionsTests.Person p)
        {
            if (p.Age < 0)
                throw new ArgumentException("Age must be >= 0");
        }
    }

    [TestClass]
    public class ServiceCollectionExtensionsTests
    {
        public class Person
        {
            public int Age { get; set; }
        }

        [TestInitialize]
        public void SetUp()
        {
            // Reset to default scoped mode before each test
            ValidationContext.Mode = ValidationContextMode.ScopedAsyncFlow;
        }

        [TestMethod]
        public void AddRuleflow_RegistersRegistryAsSingleton()
        {
            var services = new ServiceCollection();
            services.AddRuleflow<Person>();
            var provider = services.BuildServiceProvider();

            var reg1 = provider.GetRequiredService<IRuleRegistry<Person>>();
            var reg2 = provider.GetRequiredService<IRuleRegistry<Person>>();

            Assert.IsNotNull(reg1);
            Assert.AreSame(reg1, reg2);
        }

        [TestMethod]
        public void AddRuleflow_WithInitialRules_RegistersThem()
        {
            var rule = Ruleflow.NET.Engine.Models.Rule.Builder.RuleBuilderFactory
                .CreateUnifiedRuleBuilder<Person>()
                .WithValidation((p, ctx) => p.Age >= 18)
                .WithErrorMessage("Age must be at least 18")
                .Build();

            var services = new ServiceCollection();
            services.AddRuleflow<Person>(o => o.InitialRules = new[] { rule });
            var provider = services.BuildServiceProvider();

            var registry = provider.GetRequiredService<IRuleRegistry<Person>>();
            Assert.AreEqual(1, registry.Count);
        }

        [TestMethod]
        public void AddRuleflow_RegistersValidationContext_DefaultScopedMode()
        {
            var services = new ServiceCollection();
            services.AddRuleflow<Person>();
            var provider = services.BuildServiceProvider();

            // In scoped (transient) mode, resolving ValidationContext works and is not null
            var context1 = provider.GetRequiredService<ValidationContext>();
            var context2 = provider.GetRequiredService<ValidationContext>();
            Assert.IsNotNull(context1);
            Assert.IsNotNull(context2);
        }

        [TestMethod]
        public void AddRuleflow_RegistersValidationContext_LegacySingletonMode()
        {
            var services = new ServiceCollection();
            services.AddRuleflow<Person>(o => o.UseLegacyGlobalValidationContext = true);
            var provider = services.BuildServiceProvider();

            var context1 = provider.GetRequiredService<ValidationContext>();
            var context2 = provider.GetRequiredService<ValidationContext>();
            Assert.IsNotNull(context1);
            Assert.AreSame(context1, context2);
        }

        [TestMethod]
        public void AddRuleflow_RegistersValidationContextAccessor()
        {
            var services = new ServiceCollection();
            services.AddRuleflow<Person>();
            var provider = services.BuildServiceProvider();

            var accessor = provider.GetRequiredService<IValidationContextAccessor>();
            Assert.IsNotNull(accessor);
            Assert.IsNotNull(accessor.Current);
        }

        [TestMethod]
        public void AddRuleflow_RegisterDefaultValidator_AddsValidator()
        {
            var services = new ServiceCollection();
            services.AddRuleflow<Person>(o => o.RegisterDefaultValidator = true);
            var provider = services.BuildServiceProvider();

            var validator1 = provider.GetRequiredService<IValidator<Person>>();
            var validator2 = provider.GetRequiredService<IValidator<Person>>();

            Assert.IsNotNull(validator1);
            Assert.AreSame(validator1, validator2);
        }

        [TestMethod]
        public void AddRuleflow_WithoutRegisterDefaultValidator_NoValidatorRegistered()
        {
            var services = new ServiceCollection();
            services.AddRuleflow<Person>();
            var provider = services.BuildServiceProvider();

            var validator = provider.GetService<IValidator<Person>>();
            Assert.IsNull(validator);
        }

        [TestMethod]
        public void AddRuleflow_WithProfile_RegistersMapperAndValidator()
        {
            var profile = new RuleflowProfile<Person>();
            profile.MappingRules.Add(new DataMappingRule<Person>(p => p.Age, "age", DataType.Int32));
            profile.ValidationRules.Add(RuleflowExtensions.CreateRule<Person>()
                .WithAction(p => { if (p.Age < 0) throw new ArgumentException(); })
                .Build());

            var services = new ServiceCollection();
            services.AddRuleflow<Person>(o => o.RegisterDefaultValidator = true, profile);
            var provider = services.BuildServiceProvider();

            Assert.IsNotNull(provider.GetRequiredService<IDataAutoMapper<Person>>());
            Assert.IsNotNull(provider.GetRequiredService<IValidator<Person>>());
        }

        [TestMethod]
        public void AddRuleflow_WithLoggerFactory_UsesProvidedFactory()
        {
            var services = new ServiceCollection();
            var loggerFactory = LoggerFactory.Create(builder => { });

            services.AddRuleflow<Person>(o => o.LoggerFactory = loggerFactory);

            var provider = services.BuildServiceProvider();

            _ = provider.GetRequiredService<IRuleRegistry<Person>>();

            Assert.AreNotSame(NullLogger<EventHub.EventHubLog>.Instance, EventHub.Logger);
        }

        [TestMethod]
        public async Task AddRuleflow_RegistersMediatorHandlers_ForRuleCommandsAndQueries()
        {
            var services = new ServiceCollection();
            services.AddRuleflow<Person>();
            var provider = services.BuildServiceProvider();

            var mediator = provider.GetRequiredService<IMediator>();

            var rule = Ruleflow.NET.Engine.Models.Rule.Builder.RuleBuilderFactory
                .CreateUnifiedRuleBuilder<Person>()
                .WithRuleId("person.age.rule")
                .WithValidation((p, ctx) => p.Age >= 18)
                .Build();

            var registered = await mediator.Send(new RegisterRuleCommand<Person>(rule));
            Assert.IsTrue(registered);

            var fetched = await mediator.Send(new GetRuleByIdQuery<Person>("person.age.rule"));
            Assert.IsNotNull(fetched);
            Assert.AreEqual("person.age.rule", fetched.RuleId);

            var all = await mediator.Send(new GetAllRulesQuery<Person>());
            Assert.AreEqual(1, all.Count);

            var unregistered = await mediator.Send(new UnregisterRuleCommand<Person>("person.age.rule"));
            Assert.IsTrue(unregistered);
        }

        [TestMethod]
        public void AddRuleflow_AutoRegisterAttributeRules_IncludedInDefaultValidator()
        {
            var services = new ServiceCollection();
            services.AddRuleflow<Person>(o =>
            {
                o.RegisterDefaultValidator = true;
                o.AutoRegisterAttributeRules = true;
                o.AssemblyFilters = new[] { typeof(PersonAttributeRules).Assembly.GetName().Name! };
            });
            var provider = services.BuildServiceProvider();

            var validator = provider.GetRequiredService<IValidator<Person>>();
            var result = validator.CollectValidationResults(new Person { Age = -1 });

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.AreEqual("age.nonnegative", result.Errors[0].Code);
            Assert.IsTrue(result.Errors[0].Message.Contains("Age must be"));
        }
    }
}
