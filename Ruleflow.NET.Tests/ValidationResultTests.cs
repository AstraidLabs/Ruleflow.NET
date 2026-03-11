using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ruleflow.NET.Engine.Validation.Core.Results;
using Ruleflow.NET.Engine.Validation.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ruleflow.NET.Tests
{
    /// <summary>
    /// Testovací třída pro ověření hraničních případů třídy ValidationResult.
    /// Tyto testy se zaměřují na okrajové situace, které mohou nastat v produkčním prostředí
    /// a zajišťují, že systém reaguje předvídatelným způsobem i v nestandardních případech.
    /// </summary>
    [TestClass]
    public class ValidationResultBoundaryTests
    {
        /// <summary>
        /// Test ověřuje, že pokus o přidání null reference jako ValidationError 
        /// vyhodí výjimku ArgumentNullException.
        /// 
        /// Use case: Ochrana před bugem v kódu, kde by metoda 'AddError' mohla být volána s null parametrem,
        /// například když validační pravidlo nesprávně vytvoří chybový objekt nebo je null předán z jiné části kódu.
        /// </summary>
        [TestMethod]
        public void ValidationResult_AddNullError_ThrowsArgumentNullException()
        {
            // Arrange
            var result = new ValidationResult();
            ValidationError nullError = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => result.AddError(nullError));
        }

        /// <summary>
        /// Test ověřuje, že pokus o přidání chyby s null textem zprávy
        /// vyhodí výjimku ArgumentNullException.
        /// 
        /// Use case: Zajišťuje, že každá validační chyba musí mít definovanou zprávu,
        /// což zabraňuje situacím, kdy by uživateli byly zobrazeny prázdné chybové zprávy.
        /// </summary>
        [TestMethod]
        public void ValidationResult_AddErrorWithNullMessage_ThrowsArgumentNullException()
        {
            // Arrange
            var result = new ValidationResult();
            string nullMessage = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => result.AddError(nullMessage, ValidationSeverity.Error));
        }

        /// <summary>
        /// Test ověřuje, že prázdný řetězec jako chybová zpráva je platný vstup.
        /// 
        /// Use case: I když to není ideální UX, někdy může být potřeba přidat chybu bez textu,
        /// například když je kontext chyby dostatečný nebo když je zpráva generována později.
        /// </summary>
        [TestMethod]
        public void ValidationResult_AddErrorWithEmptyMessage_AcceptsEmptyString()
        {
            // Arrange
            var result = new ValidationResult();
            string emptyMessage = string.Empty;

            // Act
            result.AddError(emptyMessage, ValidationSeverity.Error);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.AreEqual(string.Empty, result.Errors[0].Message);
        }

        /// <summary>
        /// Test ověřuje, že systém správně zpracuje velmi dlouhou chybovou zprávu.
        /// 
        /// Use case: V některých případech může být chybová zpráva generována dynamicky
        /// a může obsahovat velké množství dat, například výpis XML/JSON dokumentu nebo
        /// stopu zásobníku (stack trace). Systém by měl takové případy zvládnout bez omezení.
        /// </summary>
        [TestMethod]
        public void ValidationResult_AddErrorWithExtremelyLongMessage_HandlesCorrectly()
        {
            // Arrange
            var result = new ValidationResult();
            string longMessage = new string('A', 10000); // 10,000 characters

            // Act
            result.AddError(longMessage, ValidationSeverity.Error);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.AreEqual(10000, result.Errors[0].Message.Length);
            Assert.AreEqual(longMessage, result.Errors[0].Message);
        }

        /// <summary>
        /// Test ověřuje, že systém zvládne zpracovat velké množství chyb.
        /// 
        /// Use case: Při validaci komplexních dat (např. velká dávka záznamů, složitý formulář)
        /// může být vygenerováno velké množství chyb. Systém by měl zvládnout jejich uložení a zpracování
        /// bez výkonnostních problémů.
        /// </summary>
        [TestMethod]
        public void ValidationResult_AddLargeNumberOfErrors_HandlesCorrectly()
        {
            // Arrange
            var result = new ValidationResult();
            const int errorCount = 1000;

            // Act
            for (int i = 0; i < errorCount; i++)
            {
                result.AddError($"Error {i}", ValidationSeverity.Error);
            }

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(errorCount, result.Errors.Count);
        }

        /// <summary>
        /// Test ověřuje, že filtrace chyb podle závažnosti vrátí prázdnou kolekci,
        /// pokud požadovaná závažnost neexistuje.
        /// 
        /// Use case: Při zobrazování chyb v různých částech UI (např. kritické chyby zvýrazněně)
        /// je důležité, aby metoda pro filtrování neskončila s výjimkou, i když neexistují chyby dané závažnosti.
        /// </summary>
        [TestMethod]
        public void ValidationResult_GetErrorsBySeverity_WithNoMatchingSeverity_ReturnsEmptyCollection()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddError("Error message", ValidationSeverity.Error);

            // Act
            var verboseErrors = result.GetErrorsBySeverity(ValidationSeverity.Verbose).ToList();

            // Assert
            Assert.AreEqual(0, verboseErrors.Count);
        }

        /// <summary>
        /// Test ověřuje, že pouze chyby s vysokou závažností (Error, Critical) způsobí,
        /// že ValidationResult je považován za neplatný.
        /// 
        /// Use case: Umožňuje rozlišit mezi různými úrovněmi validačních zpráv - informativní,
        /// varování a skutečné chyby. Pouze skutečné chyby by měly blokovat pokračování procesu,
        /// zatímco varování mohou být zobrazena, ale neblokují průběh.
        /// </summary>
        [TestMethod]
        public void ValidationResult_IsValid_WithMixedSeverities_OnlyHighSeveritiesAffectValidity()
        {
            // Arrange
            var result = new ValidationResult();

            // Act
            result.AddError("Debug message", ValidationSeverity.Debug);
            result.AddError("Info message", ValidationSeverity.Information);
            result.AddError("Warning message", ValidationSeverity.Warning);

            // Assert
            Assert.IsTrue(result.IsValid, "Result should be valid with only low severity errors");

            // Add a higher severity error
            result.AddError("Error message", ValidationSeverity.Error);

            // Now it should be invalid
            Assert.IsFalse(result.IsValid, "Result should be invalid with Error severity");
        }

        /// <summary>
        /// Test ověřuje, že pouze chyby s nejvyšší závažností (Critical) způsobí,
        /// že ValidationResult má označení HasCriticalErrors=true.
        /// 
        /// Use case: Umožňuje identifikovat kritické chyby, které mohou vyžadovat speciální zpracování,
        /// například okamžité přerušení operace, logování, notifikaci nebo zvláštní zvýraznění v UI.
        /// </summary>
        [TestMethod]
        public void ValidationResult_HasCriticalErrors_OnlyHighestSeverityCounts()
        {
            // Arrange
            var result = new ValidationResult();

            // Act
            result.AddError("Error message", ValidationSeverity.Error);

            // Assert
            Assert.IsFalse(result.HasCriticalErrors, "Should not have critical errors with only Error severity");

            // Add a critical severity error
            result.AddError("Critical message", ValidationSeverity.Critical);

            // Now it should have critical errors
            Assert.IsTrue(result.HasCriticalErrors, "Should have critical errors with Critical severity");
        }

        /// <summary>
        /// Test ověřuje, že metoda ThrowIfInvalid vytvoří správný typ výjimky s informací o kritických chybách,
        /// pokud ValidationResult obsahuje kritické chyby.
        /// 
        /// Use case: Při automatickém přerušení procesu kvůli chybám je důležité rozlišit mezi běžnými
        /// a kritickými chybami, aby bylo možné adekvátně reagovat (např. logovat kritické chyby nebo zobrazit
        /// speciální upozornění uživateli).
        /// </summary>
        [TestMethod]
        public void ValidationResult_ThrowIfInvalid_WithCriticalErrors_ThrowsAggregateExceptionWithCriticalSource()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddError("Normal error", ValidationSeverity.Error);
            result.AddError("Critical error", ValidationSeverity.Critical);

            // Act & Assert
            var exception = Assert.ThrowsException<AggregateException>(() => result.ThrowIfInvalid());

            // Verify the exception message mentions critical errors
            Assert.IsTrue(exception.Message.Contains("kritickými"),
                "Exception message should mention critical errors");
        }

        /// <summary>
        /// Test ověřuje, že pokus o přidání null kolekce chyb vyhodí výjimku NullReferenceException.
        /// 
        /// Use case: Ochrana před chybami v kódu, kde by metoda AddErrors mohla být volána s null kolekcí,
        /// což by mohlo vést k neočekávanému chování. V produkčním kódu by měla být tato situace ošetřena.
        /// </summary>
        [TestMethod]
        public void ValidationResult_AddErrors_WithNullCollection_ThrowsNullReferenceException()
        {
            // Arrange
            var result = new ValidationResult();
            IEnumerable<ValidationError> nullErrors = null;

            // Act & Assert
            Assert.ThrowsException<NullReferenceException>(() => result.AddErrors(nullErrors));
        }

        /// <summary>
        /// Test ověřuje, že přidání prázdné kolekce chyb nezmění stav ValidationResult.
        /// 
        /// Use case: Zajištění, že volání metody s prázdnou kolekcí (což může být legitimní situace
        /// při zpracování dynamických dat) je bezpečné a nemá žádné nežádoucí vedlejší efekty.
        /// </summary>
        [TestMethod]
        public void ValidationResult_AddErrors_WithEmptyCollection_DoesNothing()
        {
            // Arrange
            var result = new ValidationResult();
            var emptyErrors = new List<ValidationError>();

            // Act
            result.AddErrors(emptyErrors);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        /// <summary>
        /// Test ověřuje, že metoda OnSuccess s vstupním parametrem předá tento parametr do callback funkce.
        /// 
        /// Use case: Umožňuje provádět následné zpracování validovaných dat při úspěšné validaci,
        /// například uložení validních dat do databáze, odeslání formuláře nebo provedení další operace.
        /// </summary>
        [TestMethod]
        public void ValidationResult_OnSuccessWithInput_PassesCorrectInput()
        {
            // Arrange
            var result = new ValidationResult();
            var testInput = "Test Input";
            string receivedInput = null;

            // Act
            result.OnSuccess(testInput, input => receivedInput = input);

            // Assert
            Assert.AreEqual(testInput, receivedInput);
        }

        /// <summary>
        /// Test ověřuje, že metoda OnFailure předá správnou kolekci chyb do callback funkce.
        /// 
        /// Use case: Umožňuje provádět zpracování chyb v případě neúspěšné validace,
        /// například generování chybových hlášek pro UI, logování chyb nebo pokusy o automatickou opravu dat.
        /// </summary>
        [TestMethod]
        public void ValidationResult_OnFailure_PassesCorrectErrorCollection()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddError("Error 1", ValidationSeverity.Error);
            result.AddError("Error 2", ValidationSeverity.Error);

            int receivedErrorCount = 0;

            // Act
            result.OnFailure(errors => receivedErrorCount = errors.Count);

            // Assert
            Assert.AreEqual(2, receivedErrorCount);
        }

        /// <summary>
        /// Test ověřuje, že při řetězení volání OnSuccess a OnFailure je spuštěna pouze
        /// ta správná callback funkce podle stavu ValidationResult.
        /// 
        /// Use case: Umožňuje elegantní zpracování výsledku validace pomocí fluent API,
        /// kde lze definovat jeden řetězec volání a systém automaticky provede správnou větev kódu.
        /// </summary>
        [TestMethod]
        public void ValidationResult_ChainedOnSuccessAndOnFailure_OnlyOneExecutes()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddError("Test error", ValidationSeverity.Error);

            bool successCalled = false;
            bool failureCalled = false;

            // Act
            result
                .OnSuccess(() => successCalled = true)
                .OnFailure(errors => failureCalled = true);

            // Assert
            Assert.IsFalse(successCalled, "OnSuccess should not have been called");
            Assert.IsTrue(failureCalled, "OnFailure should have been called");

            // Try with valid result
            var validResult = new ValidationResult();
            successCalled = false;
            failureCalled = false;

            validResult
                .OnSuccess(() => successCalled = true)
                .OnFailure(errors => failureCalled = true);

            Assert.IsTrue(successCalled, "OnSuccess should have been called");
            Assert.IsFalse(failureCalled, "OnFailure should not have been called");
        }

        /// <summary>
        /// Test ověřuje, že minimální a maximální hodnoty enum ValidationSeverity
        /// jsou správně zpracovány při vyhodnocování platnosti výsledku.
        /// 
        /// Use case: Zajišťuje, že celá škála závažností funguje správně, což je důležité
        /// pro systémy využívající více úrovní logování a validace (např. Debug pro vývojáře,
        /// Warning pro uživatele, Critical pro administrátory).
        /// </summary>
        [TestMethod]
        public void ValidationResult_MinAndMaxSeverityValues_ProcessedCorrectly()
        {
            // Arrange
            var result = new ValidationResult();

            // Test min severity (Verbose)
            result.AddError("Verbose message", ValidationSeverity.Verbose);
            Assert.IsTrue(result.IsValid);

            // Test max severity (Critical)
            result.AddError("Critical message", ValidationSeverity.Critical);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.HasCriticalErrors);

            // Check counts by severity
            Assert.AreEqual(1, result.GetErrorsBySeverity(ValidationSeverity.Verbose).Count());
            Assert.AreEqual(1, result.GetErrorsBySeverity(ValidationSeverity.Critical).Count());
        }

        /// <summary>
        /// Test ověřuje, že objekt předaný jako kontext chyby je zachován jako reference
        /// a lze k němu později přistupovat.
        /// 
        /// Use case: Kontext chyby může obsahovat dodatečné informace potřebné pro zpracování nebo
        /// zobrazení chyby - například původní validovaná data, ID entity, pozici v dokumentu
        /// nebo další metadata, která mohou být užitečná při diagnostice nebo nápravě chyby.
        /// </summary>
        [TestMethod]
        public void ValidationResult_ErrorWithContextObject_PreservesContextReference()
        {
            // Arrange
            var result = new ValidationResult();
            var contextObject = new TestContextObject { Id = 42, Name = "Test" };

            // Act
            result.AddError("Error with context", ValidationSeverity.Error, "TEST-ERR", contextObject);

            // Assert
            Assert.AreEqual(1, result.Errors.Count);
            var error = result.Errors[0];
            Assert.IsNotNull(error.Context);

            var retrievedContext = error.Context as TestContextObject;
            Assert.IsNotNull(retrievedContext);
            Assert.AreEqual(42, retrievedContext.Id);
            Assert.AreEqual("Test", retrievedContext.Name);
        }


        [TestMethod]
        public void ValidationResult_AddError_WithCodePathAndMetadata_StoresStructuredErrorData()
        {
            // Arrange
            var result = new ValidationResult();
            var metadata = new Dictionary<string, object?>
            {
                ["field"] = "Email",
                ["attempt"] = 2
            };

            // Act
            result.AddError(
                "Invalid email",
                ValidationSeverity.Error,
                code: "VAL-EMAIL-001",
                path: "Order.Customer.Email",
                metadata: metadata);

            // Assert
            var error = result.Errors.Single();
            Assert.AreEqual("VAL-EMAIL-001", error.Code);
            Assert.AreEqual("Order.Customer.Email", error.Path);
            Assert.AreEqual("Email", error.Metadata["field"]);
            Assert.AreEqual(2, error.Metadata["attempt"]);
        }

        [TestMethod]
        public void ValidationResult_AddError_WithMetadata_CopiesInputDictionary()
        {
            // Arrange
            var result = new ValidationResult();
            var metadata = new Dictionary<string, object?> { ["attempt"] = 1 };

            // Act
            result.AddError("Invalid", ValidationSeverity.Error, metadata: metadata);
            metadata["attempt"] = 99;

            // Assert
            var error = result.Errors.Single();
            Assert.AreEqual(1, error.Metadata["attempt"]);
            Assert.ThrowsException<NotSupportedException>(() =>
            {
                ((IDictionary<string, object?>)error.Metadata).Add("new", "value");
            });
        }

        // Helper class for context testing
        private class TestContextObject
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}