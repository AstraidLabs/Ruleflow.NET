using MediatR;
using Ruleflow.NET.Engine.Models.Rule.Interface;

namespace Ruleflow.NET.Engine.Cqrs.Queries
{
    public sealed record GetRuleByIdQuery<TInput>(string RuleId) : IRequest<IRule<TInput>?>;
}
