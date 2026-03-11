using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Ruleflow.NET.Engine.Cqrs.Commands;
using Ruleflow.NET.Engine.Registry.Interface;

namespace Ruleflow.NET.Engine.Cqrs.Handlers
{
    public sealed class RegisterRuleCommandHandler<TInput> : IRequestHandler<RegisterRuleCommand<TInput>, bool>
    {
        private readonly IRuleRegistry<TInput> _registry;

        public RegisterRuleCommandHandler(IRuleRegistry<TInput> registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Task<bool> Handle(RegisterRuleCommand<TInput> request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_registry.RegisterRule(request.Rule));
        }
    }
}
