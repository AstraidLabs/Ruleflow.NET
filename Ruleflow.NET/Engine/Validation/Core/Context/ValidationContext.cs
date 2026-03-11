using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Ruleflow.NET.Engine.Validation.Core.Context
{
    public class RuleExecutionResult
    {
        public bool Success { get; set; }
    }

    public class ValidationContext
    {
        private static readonly ValidationContext _instance = new ValidationContext();
        public static ValidationContext Instance => _instance;

        public ConcurrentDictionary<string, object> Properties { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, RuleExecutionResult> RuleResults { get; } = new(StringComparer.Ordinal);

        public void Clear()
        {
            Properties.Clear();
            RuleResults.Clear();
        }
    }
}
