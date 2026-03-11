using System.Collections.Generic;
using MediatR;
using Ruleflow.NET.Engine.Models.Rule.Interface;

namespace Ruleflow.NET.Engine.Cqrs.Queries
{
    public sealed record GetAllRulesQuery<TInput>() : IRequest<IReadOnlyList<IRule<TInput>>>;
}
