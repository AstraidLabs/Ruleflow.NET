using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using Ruleflow.NET.Engine.Data.Enums;
using Ruleflow.NET.Engine.Data;
using Ruleflow.NET.Engine.Data.Interfaces;
using Ruleflow.NET.Engine.Data.Values;
using Ruleflow.NET.Engine.Extensions;

namespace Ruleflow.NET.Engine.Data.Mapping
{
    /// <summary>
    /// Generic strict automapper for converting between dictionaries and objects.
    /// </summary>
    public class DataAutoMapper<T> : IDataAutoMapper<T>
    {
        private static readonly ConcurrentDictionary<string, Func<T, object?>> GetterCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, Action<T, object?>> SetterCache = new(StringComparer.Ordinal);
        private readonly List<DataMappingRule<T>> _rules;

        /// <summary>
        /// Creates a mapper using mapping rules discovered from
        /// <see cref="MapKeyAttribute"/> attributes on <typeparamref name="T"/>.
        /// </summary>
        public static DataAutoMapper<T> FromAttributes()
        {
            var profile = Engine.Extensions.AttributeRuleLoader.LoadProfile<T>();
            return new DataAutoMapper<T>(profile.MappingRules);
        }

        /// <summary>
        /// Explicitly clears compiled getter/setter delegate cache for <typeparamref name="T"/>.
        /// Use when mapping rules are changed dynamically.
        /// </summary>
        public static void InvalidateCompiledAccessorCache()
        {
            GetterCache.Clear();
            SetterCache.Clear();
        }

        /// <summary>
        /// Creates a new mapper with the provided rules.
        /// </summary>
        public DataAutoMapper(IEnumerable<DataMappingRule<T>> rules)
        {
            _rules = new List<DataMappingRule<T>>(rules);
        }

        /// <summary>
        /// Maps dictionary data to an object instance.
        /// </summary>
        public T MapToObject(IDictionary<string, string> data, DataContext context)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var obj = Activator.CreateInstance<T>();

            foreach (var rule in _rules)
            {
                if (!data.TryGetValue(rule.Key, out var raw))
                {
                    if (rule.IsRequired)
                        throw new DataMappingException($"Required key '{rule.Key}' missing.");
                    continue;
                }

                if (!DataConverter.TryConvert(raw, rule.Type, out var value) || value == null)
                    throw new DataMappingException($"Failed to convert key '{rule.Key}' to type {rule.Type}.");

                var setter = GetOrCreateSetter(rule);
                setter(obj, value.Value);
                context.Set(rule.Key, value);
            }

            return obj;
        }

        /// <summary>
        /// Maps an object instance to dictionary data.
        /// </summary>
        public IDictionary<string, IDataValue> MapToData(T obj, DataContext context)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var result = new Dictionary<string, IDataValue>();

            foreach (var rule in _rules)
            {
                var getter = GetOrCreateGetter(rule);
                var raw = getter(obj);
                if (raw == null)
                {
                    if (rule.IsRequired)
                        throw new DataMappingException($"Required property '{rule.Property.Name}' is null.");
                    continue;
                }

                var value = CreateValue(raw, rule.Type);
                result[rule.Key] = value;
                context.Set(rule.Key, value);
            }

            return result;
        }

        private static Func<T, object?> GetOrCreateGetter(DataMappingRule<T> rule)
        {
            return GetterCache.GetOrAdd(BuildAccessorCacheKey(rule), _ =>
            {
                var target = Expression.Parameter(typeof(T), "target");
                var property = Expression.Property(target, rule.Property);
                var convert = Expression.Convert(property, typeof(object));
                return Expression.Lambda<Func<T, object?>>(convert, target).Compile();
            });
        }

        private static Action<T, object?> GetOrCreateSetter(DataMappingRule<T> rule)
        {
            return SetterCache.GetOrAdd(BuildAccessorCacheKey(rule), _ =>
            {
                var target = Expression.Parameter(typeof(T), "target");
                var value = Expression.Parameter(typeof(object), "value");
                var property = Expression.Property(target, rule.Property);
                var assign = Expression.Assign(property, Expression.Convert(value, rule.Property.PropertyType));
                return Expression.Lambda<Action<T, object?>>(assign, target, value).Compile();
            });
        }

        private static string BuildAccessorCacheKey(DataMappingRule<T> rule)
            => $"{typeof(T).FullName}:{rule.Property.DeclaringType?.FullName}:{rule.Property.Name}";

        private static IDataValue CreateValue(object raw, DataType type)
        {
            return type switch
            {
                DataType.String => new DataValue<string>((string)raw, type),
                DataType.Int32 => new DataValue<int>(Convert.ToInt32(raw), type),
                DataType.Int64 => new DataValue<long>(Convert.ToInt64(raw), type),
                DataType.Decimal => new DataValue<decimal>(Convert.ToDecimal(raw), type),
                DataType.Double => new DataValue<double>(Convert.ToDouble(raw), type),
                DataType.Boolean => new DataValue<bool>(Convert.ToBoolean(raw), type),
                DataType.DateTime => new DataValue<DateTime>(Convert.ToDateTime(raw), type),
                DataType.Guid => new DataValue<Guid>((Guid)raw, type),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }
    }
}
