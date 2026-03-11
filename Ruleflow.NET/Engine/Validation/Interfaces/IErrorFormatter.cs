using Ruleflow.NET.Engine.Validation.Core.Results;

namespace Ruleflow.NET.Engine.Validation.Interfaces
{
    public interface IErrorFormatter
    {
        object Format(ValidationError error);
    }
}
