using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ruleflow.NET.Engine.Data.Enums;
using System.Collections.Generic;
using Ruleflow.NET.Engine.Data.Mapping;

namespace Ruleflow.NET.Tests
{
    [TestClass]
    public class DataAutoMapperTests
    {
        private class Person
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        [TestMethod]
        public void MapToObject_ValidData_Succeeds()
        {
            var rules = new[]
            {
                new DataMappingRule<Person>(p => p.Name, "name", DataType.String, true),
                new DataMappingRule<Person>(p => p.Age, "age", DataType.Int32, true)
            };

            var mapper = new DataAutoMapper<Person>(rules);
            var context = new DataContext();

            var data = new Dictionary<string, string> { { "name", "John" }, { "age", "30" } };
            var person = mapper.MapToObject(data, context);

            Assert.AreEqual("John", person.Name);
            Assert.AreEqual(30, person.Age);
            Assert.AreEqual(2, context.Values.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(DataMappingException))]
        public void MapToObject_MissingRequired_Throws()
        {
            var rules = new[] { new DataMappingRule<Person>(p => p.Name, "name", DataType.String, true) };
            var mapper = new DataAutoMapper<Person>(rules);
            var context = new DataContext();
            var data = new Dictionary<string, string>();
            mapper.MapToObject(data, context);
        }

        [TestMethod]
        public void MapToData_ValidObject_Succeeds()
        {
            var rules = new[]
            {
                new DataMappingRule<Person>(p => p.Name, "name", DataType.String, true),
                new DataMappingRule<Person>(p => p.Age, "age", DataType.Int32, true)
            };

            var mapper = new DataAutoMapper<Person>(rules);
            var context = new DataContext();
            var person = new Person { Name = "Jane", Age = 25 };

            var data = mapper.MapToData(person, context);

            Assert.AreEqual("Jane", data["name"].Value);
            Assert.AreEqual(25, data["age"].Value);
            Assert.AreEqual(2, context.Values.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(DataMappingException))]
        public void MapToObject_InvalidConversion_Throws()
        {
            var rules = new[]
            {
                new DataMappingRule<Person>(p => p.Name, "name", DataType.String, true),
                new DataMappingRule<Person>(p => p.Age, "age", DataType.Int32, true)
            };

            var mapper = new DataAutoMapper<Person>(rules);
            var context = new DataContext();

            var data = new Dictionary<string, string> { { "name", "John" }, { "age", "not-number" } };

            mapper.MapToObject(data, context);
        }

        [TestMethod]
        [ExpectedException(typeof(DataMappingException))]
        public void MapToData_RequiredPropertyNull_Throws()
        {
            var rules = new[]
            {
                new DataMappingRule<Person>(p => p.Name, "name", DataType.String, true),
                new DataMappingRule<Person>(p => p.Age, "age", DataType.Int32, true)
            };

            var mapper = new DataAutoMapper<Person>(rules);
            var context = new DataContext();

            var person = new Person { Name = null!, Age = 0 };
            mapper.MapToData(person, context);
        }

        [TestMethod]
        public void MapToData_OptionalPropertyNull_Ignored()
        {
            var rules = new[]
            {
                new DataMappingRule<Person>(p => p.Name, "name", DataType.String, false),
                new DataMappingRule<Person>(p => p.Age, "age", DataType.Int32, true)
            };

            var mapper = new DataAutoMapper<Person>(rules);
            var context = new DataContext();

            var person = new Person { Name = null!, Age = 30 };
            var data = mapper.MapToData(person, context);

            Assert.IsFalse(data.ContainsKey("name"));
            Assert.AreEqual(30, data["age"].Value);
            Assert.AreEqual(1, context.Values.Count);
        }

        [TestMethod]
        public void InvalidateCompiledAccessorCache_AllowsRemappingAfterCacheReset()
        {
            var rules = new[]
            {
                new DataMappingRule<Person>(p => p.Name, "name", DataType.String, true),
                new DataMappingRule<Person>(p => p.Age, "age", DataType.Int32, true)
            };

            var mapper = new DataAutoMapper<Person>(rules);
            var context = new DataContext();

            var first = mapper.MapToData(new Person { Name = "Before", Age = 20 }, context);
            Assert.AreEqual("Before", first["name"].Value);

            DataAutoMapper<Person>.InvalidateCompiledAccessorCache();

            var second = mapper.MapToObject(
                new Dictionary<string, string> { { "name", "After" }, { "age", "21" } },
                new DataContext());

            Assert.AreEqual("After", second.Name);
            Assert.AreEqual(21, second.Age);
        }

    }
}
