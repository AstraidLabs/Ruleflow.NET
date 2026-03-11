using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Ruleflow.NET.Engine.Validation.Enums;
using Ruleflow.NET.Engine.Validation.Interfaces;

namespace Ruleflow.NET.Engine.Validation.Core.Results
{
    /// <summary>
    /// Represents aggregated validation result containing all validation errors.
    /// </summary>
    public class ValidationResult
    {
        private readonly List<ValidationError> _errors = new();
        public IReadOnlyList<ValidationError> Errors => _errors;

        public bool IsValid => !_errors.Any(e => e.Severity >= ValidationSeverity.Error);
        public bool HasCriticalErrors => _errors.Any(e => e.Severity == ValidationSeverity.Critical);

        /// <summary>
        /// Přidá jednu chybovou zprávu do výsledku.
        /// <para>Adds a single validation error to the result.</para>
        /// </summary>
        public void AddError(ValidationError error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            _errors.Add(error);
        }

        /// <summary>
        /// Vytvoří a přidá chybovou zprávu podle zadaných parametrů.
        /// <para>Creates and adds a validation error with the given information.</para>
        /// </summary>
        public void AddError(
            string message,
            ValidationSeverity severity,
            string? code = null,
            object? context = null,
            string? path = null,
            IReadOnlyDictionary<string, object?>? metadata = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var readonlyMetadata = metadata == null
                ? null
                : new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(metadata));
            _errors.Add(new ValidationError(message, severity, code, context, path, readonlyMetadata));
        }

        /// <summary>
        /// Přidá do výsledku více chybových zpráv najednou.
        /// <para>Adds multiple validation errors to the result.</para>
        /// </summary>
        public void AddErrors(IEnumerable<ValidationError> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            foreach (var e in errors) AddError(e);
        }

        /// <summary>
        /// Vrátí chyby jen s danou úrovní závažnosti.
        /// <para>Returns errors filtered by severity.</para>
        /// </summary>
        public IEnumerable<ValidationError> GetErrorsBySeverity(ValidationSeverity severity)
        {
            return _errors.Where(e => e.Severity == severity);
        }

        /// <summary>
        /// Spustí akci pokud je výsledek validace úspěšný.
        /// <para>Invokes the supplied action if the validation succeeded.</para>
        /// </summary>
        public ValidationResult OnSuccess(Action action)
        {
            if (IsValid) action();
            return this;
        }

        /// <summary>
        /// Spustí akci s parametrem, pokud je výsledek validace úspěšný.
        /// <para>Invokes the given action with the input when validation succeeded.</para>
        /// </summary>
        public ValidationResult OnSuccess<T>(T input, Action<T> action)
        {
            if (IsValid) action(input);
            return this;
        }

        /// <summary>
        /// Spustí akci pokud validace neuspěla.
        /// <para>Invokes the supplied action with collected errors when validation fails.</para>
        /// </summary>
        public ValidationResult OnFailure(Action<IReadOnlyList<ValidationError>> action)
        {
            if (!IsValid) action(Errors);
            return this;
        }

        /// <summary>
        /// Vyhodí výjimku při neúspěšné validaci.
        /// <para>Throws an exception when the result contains critical errors.</para>
        /// </summary>
        public void ThrowIfInvalid()
        {
            if (!IsValid)
            {
                var ex = new AggregateException("Validace selhala s kritickými chybami");
                throw ex;
            }
        }

        public IReadOnlyList<object> FormatErrors(IErrorFormatter formatter)
        {
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            return _errors.Select(formatter.Format).ToArray();
        }
    }
}
