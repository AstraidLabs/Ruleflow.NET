using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Ruleflow.NET.Engine.Cqrs.Commands;
using Ruleflow.NET.Engine.Registry.Interface;

namespace Ruleflow.NET.Engine.Cqrs.Handlers
{
    public sealed class UnregisterRuleCommandHandler<TInput> : IRequestHandler<UnregisterRuleCommand<TInput>, bool>
    {
        private readonly IRuleRegistry<TInput> _registry;

        public UnregisterRuleCommandHandler(IRuleRegistry<TInput> registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Task<bool> Handle(UnregisterRuleCommand<TInput> request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_registry.UnregisterRule(request.RuleId));
        }
    }
}
