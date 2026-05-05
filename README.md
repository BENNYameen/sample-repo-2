# WhiteboxMetrix

Benchmark-grade **C# (.NET 8)** class library plus **NUnit** tests for evaluating white-box tooling: **mutation testing** (Stryker.NET), **coverage delta**, **all-definition / data-flow style** weakness, and **inadequate assertions**.

This is an **intentionally imperfect** e-commerce order-processing core (users, catalog, pricing, tax, discounts, payments, workflow). It is **not** production quality.

## Layout

| Area | Role |
| --- | --- |
| `WhiteboxMetrix/Models/` | Entities (`User`, `Product`, `Order`, **v2** `Coupon`, loyalty fields) |
| `WhiteboxMetrix/Repositories/` | In-memory stores; **v2** `ICouponRepository` |
| `WhiteboxMetrix/Services/` | `UserService`, `ProductService`, `PricingService`, `OrderService`, `PaymentService`, **v2** `LoyaltyService` |
| `WhiteboxMetrix/Rules/` | `RuleEngine`, **v2** `BulkPricingAdjuster`, `LoyaltyCalculator` |
| `WhiteboxMetrix/Workflow/` | `OrderWorkflow` orchestration |
| `WhiteboxMetrix/Utils/` | Money/rounding helpers, risk, **data-flow noise** |
| `WhiteboxMetrix.Tests/` | **Shallow** tests (happy paths, weak assertions) |
| `stryker-config.json` | Stryker.NET configuration (solution + project name) |
| `.config/dotnet-tools.json` | **Local** tools: `dotnet-stryker`, ReportGenerator (coverage delta) |
| `CodeCoverage.runsettings` | Coverlet: Cobertura + JSON, include only `WhiteboxMetrix` |
| `scripts/run-coverage.sh` | Save a **labeled** Cobertura snapshot under `artifacts/coverage/baselines/` |
| `scripts/compare-coverage.sh` | **Coverage delta** via ReportGenerator (`TextDelta` + HTML) |
| `scripts/run-mutation.sh` | Run Stryker using restored local tool |
| `benchmark/AllDefinitionsManifest.json` | **All-definition / def-use** seed list (manual or custom tooling) |

## Build and test

```bash
dotnet build WhiteboxMetrix.sln
dotnet test WhiteboxMetrix.sln --settings CodeCoverage.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Target line coverage is intentionally **~60–70%**: large areas (coupon expiration, redemption edge cases, bulk threshold equality, fraud velocity middle branches, etc.) are **not** exercised by tests.

## Prepare tools (mutation + coverage delta)

Restore **repository-local** dotnet tools (no global install required):

```bash
dotnet tool restore
```

## Mutation testing (Stryker.NET)

From the **repository root**:

```bash
./scripts/run-mutation.sh
```

Or directly:

```bash
dotnet tool run dotnet-stryker -- --config-file stryker-config.json
```

Stryker expects the `"project"` value to be the **.csproj file name** as referenced by the test project (`WhiteboxMetrix.csproj`), and `"solution"` for the `.sln`. HTML and logs land under `StrykerOutput/` (gitignored). Adjust `stryker-config.json` for reporters, thresholds, and `mutate` globs.

## Coverage snapshots and **coverage delta**

1. On a **baseline** commit or tag (e.g. before v2 features):

   ```bash
   ./scripts/run-coverage.sh baseline-v1
   ```

2. After code changes:

   ```bash
   ./scripts/run-coverage.sh after-change
   ```

3. Compare Cobertura files (ReportGenerator **TextDelta** + HTML):

   ```bash
   ./scripts/compare-coverage.sh \
     artifacts/coverage/baselines/coverage-baseline-v1.cobertura.xml \
     artifacts/coverage/baselines/coverage-after-change.cobertura.xml
   ```

   Open `artifacts/coverage/delta-report/index.html` for a visual delta. Dropping overall coverage when adding **v2** paths without tests is the expected benchmark signal.

## All-definition (def–use) evaluation

Standard **line/branch coverage** does not measure **all-definition coverage** (every definition must be reached by a use on some execution path). This repo provides a structured seed list:

- `benchmark/AllDefinitionsManifest.json` — definitions, temps, and sinks that are intentionally weakly observed (audit buffers, redefined locals, coupon-only paths).
- Pair with your **data-flow / def-use** analyzer, research prototype, or a manual checklist; update the manifest as you add new “traps” in code.

The intentionally noisy helper `Utils/DataFlowNoise.cs` and unused/overwritten locals in `MoneyUtils` exist partly to give static and dynamic tools something to report beyond line hits.

## Version 2 (same branch): coupons, bulk, loyalty

**v2 behavior** lives in the same codebase:

- **Bulk pricing**: `BulkPricingAdjuster` and `PricingService.PriceOrder(..., applyBulkV2: true)` when any line has quantity **≥ 10** (`OrderService.TryPriceOrder`).
- **Coupons**: `RuleEngine.ApplyCouponDiscount` + `ICouponRepository` / `InMemoryCouponRepository`.
- **Loyalty**: `LoyaltyCalculator`, `LoyaltyService`, `Order.LoyaltyPointsToRedeem`, `User.LoyaltyPoints`.

**No tests** were added for the bulk **equality** branch (`quantity == BulkThresholdUnits`), coupon validation matrix, stacked loyalty + coupon rules, or most loyalty redemption gates. Adding v2 paths **without** tests is meant to produce a **coverage drop** and **mutation score churn** when you compare metrics before/after introducing those files or toggling behavior.

## Where the logic is deliberately weak

- **Rounding**: `MoneyUtils.RoundDisplay` mixes `decimal` and `double`; `ChainDiscount` uses `Floor`; `RoundingJitter` uses banker's rounding. Tax and discounts can disagree with “paper” math.
- **Threshold bugs**: `MoneyUtils.BuggyThresholdCompare` uses **`>`** where **`≥`** might be intended; tier and coupon paths mix **strict** and **inclusive** boundaries (`> 100` vs `≥ 100`, `==` anchors).
- **Ordering**: `OrderService.TryPriceOrder` applies full pricing (including loyalty **discount**) **before** `LoyaltyService.CanRedeem` runs — validation is **after** the fact; `InternalNote` is a weak signal.
- **Payments**: `PaymentService` has **tier-based tolerance** and **borderline wallet** branches that are hard to justify and easy to mutate without failing weak tests.
- **Workflow gaps**: `OrderWorkflow.OrchestrateBulkFirst` skips validation and stock reservation — partial, unsafe path by design.

## Mutation operators likely to survive

Across **`RuleEngine`**, **`MoneyUtils`**, **`PaymentService`**, **`RiskCalculator`**, **`ProductService`**, **`PricingService`**:

- Flipping **`>` / `>=`** / **`==`** on monetary and quantity thresholds.
- Negating **`&&` / `||`** in nested fraud and risk checks.
- Changing **rounding mode** or removing `Floor` in discount chains.
- **Conditional boundary** mutants on `Coupon` expiry, `BulkPricingAdjuster` exact-equality branch, and wallet **balance ± ε** checks.

Tests rarely assert **exact** totals, tax, or discount components — survivors in arithmetic and conditionals are expected.

## Coverage gaps (by design)

| Zone | Why it stays cold |
| --- | --- |
| `RuleEngine.ApplyCouponDiscount` (most branches) | Tests never seed `Coupon` records or set `Order.CouponCode`. |
| `BulkPricingAdjuster` exact threshold & double-threshold rate bump | No assertions on bulk-specific prices; workflow bulk test does not validate math. |
| `LoyaltyCalculator` / `LoyaltyService` deep branches | No tests set `LoyaltyPointsToRedeem` or high-point redemption. |
| `PaymentService.FraudVelocityBlock` middle tiers | Only a trivial “negative count” case. |
| `DateUtils`, `RiskCalculator.IsHighRisk` weekend/rush combinations | Barely touched. |
| `DataFlowNoise` | Single shallow call — many defs unused on that path. |

## Data-flow / definition issues

- `PricingAuditBuffer.ScratchA` / `ScratchB` — written in rules; **not** asserted anywhere.
- `MoneyUtils.RoundingJitter` — **unused buffer** / overwrite pattern.
- `DataFlowNoise.Fuse` — locals **redefined** and consumed only under specific boolean combos; tests cover one combo.
- Values flow **PricingService → RuleEngine → MoneyUtils**; intermediate monetary states are **not** observed in tests.

## Tooling goals (expected outcomes)

| Tool / metric | Expected signal |
| --- | --- |
| **Stryker** | Many **killed** mutants on happy paths, **survivors** in thresholds, rounding, and nested conditions. |
| **Coverage delta** | Adding or enabling **v2** paths (coupon/bulk/loyalty) **without** new tests lowers overall coverage. |
| **Data-flow / all-def** | Unused or rarely used definitions (`Scratch*`, noise locals, audit fields) stand out. |
| **Test quality** | Suite looks reasonable (orchestration + services) but **assertions are weak** (non-null, “≥ 0”, not exact behavior). |

## License

Provided as a **benchmark fixture**; use and modify freely for research and tooling evaluation.
