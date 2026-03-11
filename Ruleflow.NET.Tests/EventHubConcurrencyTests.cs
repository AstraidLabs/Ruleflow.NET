using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ruleflow.NET.Engine.Events;

namespace Ruleflow.NET.Tests
{
    [TestClass]
    public class EventHubConcurrencyTests
    {
        [TestInitialize]
        public void SetUp()
        {
            EventHub.Clear();
        }

        [TestMethod]
        public async Task EventHub_ConcurrentRegisterAndTrigger_AllHandlersRun()
        {
            const int handlerCount = 1000;
            int counter = 0;

            var tasks = new Task[handlerCount];
            for (int i = 0; i < handlerCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    EventHub.Register("ConcurrentEvent", () => Interlocked.Increment(ref counter));
                });
            }

            await Task.WhenAll(tasks);

            EventHub.Trigger("ConcurrentEvent");

            Assert.AreEqual(handlerCount, counter,
                $"Expected {handlerCount} handler invocations but got {counter}");
        }

        [TestMethod]
        public void EventHub_Register_NullEventName_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => EventHub.Register(null!, () => { }));
        }

        [TestMethod]
        public void EventHub_Register_EmptyEventName_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => EventHub.Register("", () => { }));
        }

        [TestMethod]
        public void EventHub_Register_NullHandler_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => EventHub.Register("evt", null!));
        }

        [TestMethod]
        public void EventHub_Trigger_NullEventName_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => EventHub.Trigger(null!));
        }
    }
}
