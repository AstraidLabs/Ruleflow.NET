using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Ruleflow.NET.Engine.Cqrs.Queries;
using Ruleflow.NET.Engine.Models.Rule.Interface;
using Ruleflow.NET.Engine.Registry.Interface;

namespace Ruleflow.NET.Engine.Cqrs.Handlers
{
    public sealed class GetRuleByIdQueryHandler<TInput> : IRequestHandler<GetRuleByIdQuery<TInput>, IRule<TInput>?>
    {
        private readonly IRuleRegistry<TInput> _registry;

        public GetRuleByIdQueryHandler(IRuleRegistry<TInput> registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Task<IRule<TInput>?> Handle(GetRuleByIdQuery<TInput> request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_registry.GetRuleById(request.RuleId));
        }
    }
}
