<div align="center">
  <img src="Ruleflow.NET/Engine/Images/Ruleflow.NET.png" alt="Ruleflow.NET logo" />
</div>

# ⚙️ Ruleflow.NET

Ruleflow.NET je knihovna pro definici a spouštění validačních/business pravidel v .NET. Kombinuje fluent builder API, podporu závislostí mezi pravidly, jednoduchou integraci do DI a pomocné nástroje pro mapování dat.

## ✨ Co knihovna umí

- Fluent buildery pro `Action`, `Dependent`, `Conditional`, `Switch` a `Event` pravidla.
- Validaci přes `Validator<T>` i `DependencyAwareValidator<T>`.
- Sdílený `ValidationContext` pro předávání výsledků mezi pravidly.
- Slučování a dávkové vyhodnocení (`CompositeValidator`, `BatchValidator`).
- Registraci přes `IServiceCollection` (`AddRuleflow<TInput>`).
- Automatické načítání validačních atributů a mapování (`ValidationRuleAttribute`, `MapKeyAttribute`).
- Data mapping mezi `Dictionary<string, string>` a objektem (`DataAutoMapper<T>`).

## 📦 Instalace

```bash
dotnet add package Ruleflow.NET
```

## 🚀 Rychlý start

```csharp
using System;
using Ruleflow.NET.Engine.Validation;
using Ruleflow.NET.Engine.Validation.Core.Validators;

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

var ageRule = RuleflowExtensions.CreateRule<Person>()
    .WithId("AgeRule")
    .WithAction(p =>
    {
        if (p.Age < 18)
            throw new ArgumentException("Person must be an adult");
    })
    .Build();

var validator = new Validator<Person>(new[] { ageRule });
var result = validator.CollectValidationResults(new Person { Name = "John", Age = 17 });

Console.WriteLine(result.IsValid); // false
foreach (var error in result.Errors)
    Console.WriteLine($"{error.Severity}: {error.Message}");
```

## 🧩 Pravidla se závislostmi

```csharp
using System;
using Ruleflow.NET.Engine.Validation;
using Ruleflow.NET.Engine.Validation.Core.Validators;
using Ruleflow.NET.Engine.Validation.Enums;

public class SignUpModel
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

var usernameRule = RuleflowExtensions.CreateRule<SignUpModel>()
    .WithId("UsernameValidation")
    .WithAction(m =>
    {
        if (string.IsNullOrWhiteSpace(m.Username) || m.Username.Length < 3)
            throw new ArgumentException("Username must have at least 3 characters");
    })
    .Build();

var passwordRule = RuleflowExtensions.CreateRule<SignUpModel>()
    .WithId("PasswordValidation")
    .WithAction(m =>
    {
        if (string.IsNullOrWhiteSpace(m.Password) || m.Password.Length < 8)
            throw new ArgumentException("Password must have at least 8 characters");
    })
    .Build();

var emailRule = RuleflowExtensions.CreateDependentRule<SignUpModel>("EmailValidation")
    .DependsOn("UsernameValidation", "PasswordValidation")
    .WithDependencyType(DependencyType.RequiresAllSuccess)
    .WithAction(m =>
    {
        if (string.IsNullOrWhiteSpace(m.Email) || !m.Email.Contains("@"))
            throw new ArgumentException("Valid email is required");
    })
    .Build();

var validator = new DependencyAwareValidator<SignUpModel>(new[]
{
    usernameRule,
    passwordRule,
    emailRule
});

var result = validator.CollectValidationResults(new SignUpModel
{
    Username = "jo",
    Password = "pw",
    Email = "invalid"
});
```

## 🔀 Conditional a Switch pravidla

```csharp
using System;
using Ruleflow.NET.Engine.Validation;
using Ruleflow.NET.Engine.Validation.Core.Validators;

public class Order
{
    public decimal Amount { get; set; }
    public string CountryCode { get; set; } = "CZ";
}

var conditionalRule = RuleflowExtensions.CreateConditionalRule<Order>(o => o.Amount > 1000)
    .WithId("HighValueOrderRule")
    .Then(b => b.WithAction(o =>
    {
        if (o.CountryCode != "CZ")
            throw new ArgumentException("High value order must be domestic");
    }))
    .Else(b => b.WithAction(o =>
    {
        if (o.Amount < 0)
            throw new ArgumentException("Amount cannot be negative");
    }))
    .Build();

var switchRule = RuleflowExtensions.CreateSwitchRule<Order, string>(o => o.CountryCode)
    .WithId("CountryRule")
    .Case("CZ", b => b.WithAction(_ => { }))
    .Case("SK", b => b.WithAction(_ => { }))
    .Default(b => b.WithAction(_ => throw new ArgumentException("Unsupported country")))
    .Build();

var validator = new Validator<Order>(new[] { conditionalRule, switchRule });
```

## 🔔 Event pravidla

```csharp
using Ruleflow.NET.Engine.Events;
using Ruleflow.NET.Engine.Validation;
using Ruleflow.NET.Engine.Validation.Core.Validators;

bool triggered = false;
EventHub.Clear();
EventHub.Register("OrderValidated", () => triggered = true);

var eventRule = RuleflowExtensions.CreateEventRule<object>("OrderValidated").Build();
var validator = new Validator<object>(new[] { eventRule });
validator.CollectValidationResults(new object());

// triggered == true
```

## 🧭 DI integrace (`Microsoft.Extensions.DependencyInjection`)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Ruleflow.NET.Engine.Validation.Interfaces;
using Ruleflow.NET.Extensions;

var services = new ServiceCollection();
services.AddLogging();

services.AddRuleflow<Person>(options =>
{
    options.RegisterDefaultValidator = true;
    options.AutoRegisterAttributeRules = true;
    options.AutoRegisterMappings = true;
});

var provider = services.BuildServiceProvider();
var validator = provider.GetRequiredService<IValidator<Person>>();
```

## 🗺️ Data mapping (`DataAutoMapper<T>`)

```csharp
using System.Collections.Generic;
using Ruleflow.NET.Engine.Data.Enums;
using Ruleflow.NET.Engine.Data.Mapping;

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

var mappingRules = new[]
{
    new DataMappingRule<Person>(p => p.Name, "name", DataType.String, true),
    new DataMappingRule<Person>(p => p.Age, "age", DataType.Int32, true)
};

var mapper = new DataAutoMapper<Person>(mappingRules);
var context = new DataContext();

var data = new Dictionary<string, string>
{
    ["name"] = "Jane",
    ["age"] = "25"
};

var person = mapper.MapToObject(data, context);
var serialized = mapper.MapToData(person, context);
```

## 🧪 Testy

```bash
dotnet test
```

## 📚 Dokumentace

Doporučený postup konfigurace a rozšířené poznámky najdete v dokumentu [`Ruleflow Codex`](docs/RuleflowCodex.md).

## 🤝 Contributing

Issues i pull requesty jsou vítané.

## 📄 License

Projekt je licencován pod [Apache License 2.0](LICENSE.txt).
