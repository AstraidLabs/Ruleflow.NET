using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Ruleflow.NET.Engine.Models.Rule.Interface;

namespace Ruleflow.NET.Extensions
{
    /// <summary>
    /// Options used when configuring Ruleflow services.
    /// </summary>
    public class RuleflowOptions<TInput>
    {
        /// <summary>
        /// Initial rules that will be added to the registry.
        /// </summary>
        public IEnumerable<IRule<TInput>>? InitialRules { get; set; }

        /// <summary>
        /// If true, a default <see cref="IValidator{TInput}"/> built from registered rules will be registered.
        /// </summary>
        public bool RegisterDefaultValidator { get; set; }

        /// <summary>
        /// When true, validation rules annotated with <see cref="Engine.Validation.ValidationRuleAttribute"/>
        /// will be automatically loaded during <c>AddRuleflow</c>.
        /// </summary>
        public bool AutoRegisterAttributeRules { get; set; }

        /// <summary>
        /// When true, a <see cref="Engine.Data.Mapping.IDataAutoMapper{TInput}"/> built from
        /// <see cref="Engine.Data.Mapping.MapKeyAttribute"/> annotations will be registered.
        /// </summary>
        public bool AutoRegisterMappings { get; set; }

        /// <summary>
        /// Optional list of assembly names to restrict attribute scanning.
        /// If empty, the assembly containing <typeparamref name="TInput"/> is scanned.
        /// </summary>
        public IEnumerable<string>? AssemblyFilters { get; set; }

        /// <summary>
        /// Optional list of namespace prefixes to restrict which types are scanned
        /// for attribute based rules.
        /// </summary>
        public IEnumerable<string>? NamespaceFilters { get; set; }

        /// <summary>
        /// Optional logger factory used when initializing Ruleflow services.
        /// If null, any <see cref="ILoggerFactory"/> registered in the service
        /// provider will be used, falling back to <see cref="NullLoggerFactory"/>.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; set; }

        /// <summary>
        /// When true, the legacy global singleton <see cref="Engine.Validation.Core.Context.ValidationContext"/>
        /// is used instead of per-run scoped contexts. Default is false (scoped mode).
        /// Set to true for backwards compatibility if your code relies on the global shared context.
        /// </summary>
        public bool UseLegacyGlobalValidationContext { get; set; } = false;
    }
}
