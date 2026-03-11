using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Ruleflow.NET.Engine.Cqrs.Queries;
using Ruleflow.NET.Engine.Models.Rule.Interface;
using Ruleflow.NET.Engine.Registry.Interface;

namespace Ruleflow.NET.Engine.Cqrs.Handlers
{
    public sealed class GetAllRulesQueryHandler<TInput> : IRequestHandler<GetAllRulesQuery<TInput>, IReadOnlyList<IRule<TInput>>>
    {
        private readonly IRuleRegistry<TInput> _registry;

        public GetAllRulesQueryHandler(IRuleRegistry<TInput> registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Task<IReadOnlyList<IRule<TInput>>> Handle(GetAllRulesQuery<TInput> request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_registry.AllRules);
        }
    }
}
