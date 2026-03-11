using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ruleflow.NET.Engine.Events;
using Ruleflow.NET.Engine.Extensions;
using Ruleflow.NET.Engine.Models.Rule.Builder;
using Ruleflow.NET.Engine.Models.Rule.Interface;
using Ruleflow.NET.Engine.Registry;
using Ruleflow.NET.Engine.Registry.Interface;
using Ruleflow.NET.Engine.Validation.Core.Context;
using Ruleflow.NET.Engine.Validation.Interfaces;
using Ruleflow.NET.Engine.Validation.Core.Validators;
using Ruleflow.NET.Engine.Validation;
using Ruleflow.NET.Engine.Data.Mapping;
using Ruleflow.NET.Engine.Validation.Core.Base;

namespace Ruleflow.NET.Extensions
{
    /// <summary>
    /// Extension methods for registering Ruleflow services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds core Ruleflow services to the DI container.
        /// </summary>
        /// <typeparam name="TInput">Type of objects being validated.</typeparam>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="configure">Optional configuration for initial rules.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddRuleflow<TInput>(this IServiceCollection services,
            Action<RuleflowOptions<TInput>>? configure = null,
            params RuleflowProfile<TInput>[] profiles)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var options = new RuleflowOptions<TInput>();
            configure?.Invoke(options);

            // Apply validation context mode based on options
            ValidationContext.Mode = options.UseLegacyGlobalValidationContext
                ? ValidationContextMode.GlobalSingleton
                : ValidationContextMode.ScopedAsyncFlow;

            // Eagerly load attribute rules so they can be shared between registry and validator
            var attributeRules = options.AutoRegisterAttributeRules
                ? LoadAttributeRules(options).ToArray()
                : Array.Empty<IValidationRule<TInput>>();

            services.AddSingleton(options);
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

            services.AddSingleton<IRuleRegistry<TInput>>(sp =>
            {
                var loggerFactory = options.LoggerFactory ?? sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                var logger = loggerFactory.CreateLogger<RuleRegistry<TInput>>();
                var reg = new RuleRegistry<TInput>(options.InitialRules ?? Array.Empty<IRule<TInput>>(), logger);

                // Register attribute-discovered rules as IRule<TInput> wrappers in the registry
                foreach (var vr in attributeRules)
                {
                    var wrapper = RuleBuilderFactory.CreateUnifiedRuleBuilder<TInput>()
                        .WithRuleId(vr.Id)
                        .WithPriority(vr.Priority)
                        .WithValidation((input, ctx) =>
                        {
                            try
                            {
                                vr.Validate(input);
                                return true;
                            }
                            catch
                            {
                                return false;
                            }
                        })
                        .Build();

                    reg.RegisterRule(wrapper);
                }

                EventHub.SetLogger(loggerFactory.CreateLogger<EventHub.EventHubLog>());
                return reg;
            });

            var mappingRules = new List<DataMappingRule<TInput>>();
            if (options.AutoRegisterMappings)
                mappingRules.AddRange(AttributeRuleLoader.LoadMappingRules<TInput>());
            foreach (var profile in profiles)
            {
                services.AddSingleton(profile);
                mappingRules.AddRange(profile.MappingRules);
            }
            if (mappingRules.Count > 0)
            {
                services.AddSingleton<IDataAutoMapper<TInput>>(_ => new DataAutoMapper<TInput>(mappingRules));
            }

            // Register ValidationContext - transient in scoped mode (returns per-run context),
            // singleton in legacy mode (returns the global instance).
            if (options.UseLegacyGlobalValidationContext)
            {
                services.AddSingleton<ValidationContext>(_ => ValidationContext.Instance);
            }
            else
            {
                services.AddTransient<ValidationContext>(_ => ValidationContext.Instance);
            }

            // Register IValidationContextAccessor as singleton - always safe since it delegates to Instance
            services.AddSingleton<IValidationContextAccessor, ValidationContextAccessor>();

            // Register a default validator if requested by options
            if (options.RegisterDefaultValidator)
            {
                services.AddSingleton<IValidator<TInput>>(sp =>
                {
                    var reg = sp.GetRequiredService<IRuleRegistry<TInput>>();
                    var validationRules = new List<IValidationRule<TInput>>();
                    var seen = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var rule in reg.AllRules)
                    {
                        if (rule is IValidationRule<TInput> vr && seen.Add(vr.Id))
                            validationRules.Add(vr);
                    }
                    foreach (var profile in profiles)
                    {
                        foreach (var vr in profile.ValidationRules)
                        {
                            if (seen.Add(vr.Id))
                                validationRules.Add(vr);
                        }
                    }
                    // Include attribute-discovered validation rules (deduplicated)
                    foreach (var vr in attributeRules)
                    {
                        if (seen.Add(vr.Id))
                            validationRules.Add(vr);
                    }
                    var loggerFactory = options.LoggerFactory ?? sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                    var logger = loggerFactory.CreateLogger<Validator<TInput>>();
                    return new Validator<TInput>(validationRules, logger);
                });
            }

            return services;
        }

        private static IEnumerable<IValidationRule<TInput>> LoadAttributeRules<TInput>(RuleflowOptions<TInput> options)
        {
            var assemblies = ResolveAssemblies(options);
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (options.NamespaceFilters != null && !options.NamespaceFilters.Any(ns => type.Namespace != null && type.Namespace.StartsWith(ns)))
                        continue;

                    foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var attr = method.GetCustomAttribute<ValidationRuleAttribute>();
                        if (attr == null) continue;

                        var parameters = method.GetParameters();
                        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(TInput))
                            continue;
                        if (method.ReturnType != typeof(void))
                            continue;

                        var action = (Action<TInput>)Delegate.CreateDelegate(typeof(Action<TInput>), method);
                        var rule = new ActionValidationRule<TInput>(attr.Id, action);
                        rule.SetPriority(attr.Priority);
                        rule.SetSeverity(attr.Severity);
                        yield return rule;
                    }
                }
            }
        }

        private static IEnumerable<Assembly> ResolveAssemblies<TInput>(RuleflowOptions<TInput> options)
        {
            if (options.AssemblyFilters != null && options.AssemblyFilters.Any())
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => options.AssemblyFilters.Contains(a.GetName().Name, StringComparer.OrdinalIgnoreCase));
            }

            return new[] { typeof(TInput).Assembly };
        }
    }
}
