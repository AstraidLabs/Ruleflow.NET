using MediatR;

namespace Ruleflow.NET.Engine.Cqrs.Commands
{
    public sealed record UnregisterRuleCommand<TInput>(string RuleId) : IRequest<bool>;
}
