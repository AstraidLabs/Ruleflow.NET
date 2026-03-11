# Návrh vylepšení enginu Ruleflow.NET

Tento dokument shrnuje konkrétní návrhy, které mohou zvýšit výkon, rozšiřitelnost i ergonomii používání Ruleflow.NET. Návrhy jsou rozdělené podle priority a odhadu implementační náročnosti.

## 1) Priorita: Vysoká

### 1.1 Kompilace pravidel do exekučního plánu
**Problém:** Při každém volání validátoru se znovu řeší pořadí a závislosti pravidel.

**Návrh:**
- Při sestavení validatoru zkompilovat pravidla do immutable plánu (`ExecutionPlan<T>`), který bude obsahovat:
  - topologicky seřazené kroky,
  - informace o podmínkách spuštění,
  - přímé reference na delegáty pravidel.
- Plán znovu použít pro každé vyhodnocení.

**Přínos:** Nižší runtime overhead, stabilnější latence, jednodušší profilace.

### 1.2 Asynchronní pipeline (`IAsyncRule<T>`, `IAsyncValidator<T>`)
**Problém:** Integrace se službami (DB/API) vyžaduje asynchronní I/O, ale synchronní model to komplikuje.

**Návrh:**
- Přidat asynchronní varianty rozhraní a builderů.
- Zachovat kompatibilitu: sync pravidla adaptovat do async (`Task.CompletedTask`).
- Doplnit `CancellationToken` až do každého kroku pipeline.

**Přínos:** Přirozené napojení na moderní .NET aplikace bez blokování vláken.

### 1.3 Standardizace chyb a diagnostiky
**Problém:** Chybové zprávy jsou dobře čitelné pro člověka, ale hůř strojově zpracovatelné.

**Návrh:**
- Rozšířit `ValidationError` o:
  - `Code` (stabilní identifikátor),
  - `Path` (např. `Order.Customer.Email`),
  - `Metadata` (`Dictionary<string, object?>`).
- Přidat volitelný `IErrorFormatter` pro mapování na API kontrakt (RFC7807, GraphQL errors).

**Přínos:** Snadná lokalizace, API serializace a analytika chyb.

## 2) Priorita: Střední

### 2.1 Deterministické paralelní vyhodnocení nezávislých pravidel
**Problém:** Nezávislá pravidla se vyhodnocují sekvenčně.

**Návrh:**
- Rozdělit plán na "stages" podle závislostí.
- V rámci stage spouštět pravidla paralelně (volitelně, s limitem stupně paralelismu).
- Zajistit deterministické řazení výsledků (podle ID pravidla nebo pořadí registrace).

**Přínos:** Vyšší propustnost při větším počtu pravidel, zachovaná reprodukovatelnost výsledků.

### 2.2 Caching kompilovaných výrazů v mapování dat
**Problém:** `DataAutoMapper<T>` může opakovaně kompilovat expression stromy.

**Návrh:**
- Zavést interní cache setter/getter delegátů podle typu a property path.
- Umožnit explicitní invalidaci cache při dynamické změně pravidel.

**Přínos:** Zrychlení mapování při dávkovém zpracování.

### 2.3 Lepší observabilita (OpenTelemetry)
**Problém:** Chybí jednotný způsob, jak měřit chování pravidel v produkci.

**Návrh:**
- Instrumentace přes `ActivitySource` + `Meter`:
  - doba běhu pravidla,
  - počet selhání,
  - počet přeskočených pravidel (kvůli dependency gate).
- Přidat lightweight hooky: `OnRuleStart`, `OnRuleSuccess`, `OnRuleFailure`, `OnRuleSkipped`.

**Přínos:** Rychlejší troubleshooting a kapacitní plánování.

## 3) Priorita: Nízká

### 3.1 Source generator pro attribute-based pravidla
**Problém:** Reflection při startu aplikace může být drahá.

**Návrh:**
- Vygenerovat registraci pravidel při buildu (Roslyn source generator).
- Reflection ponechat jako fallback.

**Přínos:** Rychlejší startup a lepší AOT kompatibilita.

### 3.2 Verzionování pravidel
**Problém:** V enterprise prostředí bývá potřeba držet více verzí pravidel vedle sebe.

**Návrh:**
- Přidat `RuleVersion` a `RuleSet` koncept.
- Umožnit runtime výběr verze podle kontextu (tenant, datum účinnosti, feature flag).

**Přínos:** Bezpečnější rollout změn pravidel.

---

## Doporučený rollout plán

### Fáze A (1–2 sprinty)
1. Exekuční plán + benchmarky.
2. Rozšíření `ValidationError` o `Code`/`Path`.
3. Základní telemetry hooky.

### Fáze B (2–3 sprinty)
1. Async pipeline s `CancellationToken`.
2. Paralelní stage executor (opt-in).
3. Caching v `DataAutoMapper<T>`.

### Fáze C (volitelné)
1. Source generator pro atributy.
2. Verzionování pravidel.

## Akceptační kritéria pro první iteraci

- P95 latence validace klesne minimálně o 20 % u scénáře s 50+ pravidly.
- Veřejné API zůstane zpětně kompatibilní (minimálně na úrovni major verze).
- Telemetrie pokryje minimálně: počet pravidel, dobu validace, počet chyb.
- Přidány benchmarky a regresní testy pro dependency scénáře.
