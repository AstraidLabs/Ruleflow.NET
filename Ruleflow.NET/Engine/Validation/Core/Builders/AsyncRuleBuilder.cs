using System;
using System.Threading;
using System.Threading.Tasks;
using Ruleflow.NET.Engine.Validation.Core.Base;
using Ruleflow.NET.Engine.Validation.Enums;

namespace Ruleflow.NET.Engine.Validation.Core.Builders
{
    public class AsyncRuleBuilder<T>
    {
        private string _id = Guid.NewGuid().ToString();
        private int _priority;
        private ValidationSeverity _severity = ValidationSeverity.Error;
        private Func<T, CancellationToken, Task>? _action;

        public AsyncRuleBuilder<T> WithId(string id) { _id = id; return this; }
        public AsyncRuleBuilder<T> WithPriority(int p) { _priority = p; return this; }
        public AsyncRuleBuilder<T> WithSeverity(ValidationSeverity s) { _severity = s; return this; }
        public AsyncRuleBuilder<T> WithAction(Func<T, CancellationToken, Task> act) { _action = act; return this; }

        public AsyncActionValidationRule<T> Build()
        {
            if (_action == null) throw new InvalidOperationException("Action not set");
            var rule = new AsyncActionValidationRule<T>(_id, _action);
            rule.SetPriority(_priority);
            rule.SetSeverity(_severity);
            return rule;
        }
    }
}
