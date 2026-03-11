using MediatR;
using Ruleflow.NET.Engine.Models.Rule.Interface;

namespace Ruleflow.NET.Engine.Cqrs.Commands
{
    public sealed record UpdateRuleCommand<TInput>(IRule<TInput> Rule) : IRequest<bool>;
}
