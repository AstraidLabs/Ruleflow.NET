using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ruleflow.NET.Engine.Events
{
    /// <summary>
    /// Simple in-memory event hub for registering and triggering events.
    /// </summary>
    public static class EventHub
    {
        private static readonly ConcurrentDictionary<string, ConcurrentBag<Action>> _handlers = new(StringComparer.Ordinal);
        public class EventHubLog {}
        public static ILogger<EventHubLog> Logger { get; private set; } = NullLogger<EventHubLog>.Instance;

        public static void SetLogger(ILogger<EventHubLog>? logger)
        {
            Logger = logger ?? NullLogger<EventHubLog>.Instance;
        }

        /// <summary>
        /// Registers a handler for the specified event.
        /// </summary>
        public static void Register(string eventName, Action handler)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Event name must not be null or whitespace.", nameof(eventName));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Logger.LogInformation("Registering handler for event {Event}", eventName);
            var bag = _handlers.GetOrAdd(eventName, _ => new ConcurrentBag<Action>());
            bag.Add(handler);
        }

        /// <summary>
        /// Triggers an event and invokes all registered handlers.
        /// </summary>
        public static void Trigger(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Event name must not be null or whitespace.", nameof(eventName));

            Logger.LogInformation("Triggering event {Event}", eventName);
            if (_handlers.TryGetValue(eventName, out var bag))
            {
                foreach (var h in bag)
                {
                    try
                    {
                        h();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Event handler for {Event} failed", eventName);
                    }
                }
            }
        }

        /// <summary>
        /// Clears all registered events and handlers.
        /// </summary>
        public static void Clear()
        {
            Logger.LogInformation("Clearing all event handlers");
            _handlers.Clear();
        }
    }
}
