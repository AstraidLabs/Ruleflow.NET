using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Ruleflow.NET.Engine.Cqrs.Commands;
using Ruleflow.NET.Engine.Registry.Interface;

namespace Ruleflow.NET.Engine.Cqrs.Handlers
{
    public sealed class UpdateRuleCommandHandler<TInput> : IRequestHandler<UpdateRuleCommand<TInput>, bool>
    {
        private readonly IRuleRegistry<TInput> _registry;

        public UpdateRuleCommandHandler(IRuleRegistry<TInput> registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Task<bool> Handle(UpdateRuleCommand<TInput> request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_registry.UpdateRule(request.Rule));
        }
    }
}
