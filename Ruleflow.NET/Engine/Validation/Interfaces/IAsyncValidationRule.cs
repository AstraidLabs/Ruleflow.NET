using System.Threading;
using System.Threading.Tasks;
using Ruleflow.NET.Engine.Validation.Enums;

namespace Ruleflow.NET.Engine.Validation.Interfaces
{
    public interface IAsyncValidationRule<T>
    {
        string Id { get; }
        int Priority { get; }
        ValidationSeverity Severity { get; }
        Task ValidateAsync(T input, CancellationToken cancellationToken = default);
    }
}
