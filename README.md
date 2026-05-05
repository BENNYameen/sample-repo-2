# WhiteboxMetrix

Benchmark-grade **C# (.NET 8)** class library plus **NUnit** tests. The codebase keeps **realistic complexity** (pricing, tax, coupons, loyalty, payments); the test suite includes both historical shallow checks and **`GateCoverageTests`** aimed at **CI quality gates**: **~80% line** and **strong branch totals in Cobertura**, plus **stable `coverage.cobertura.xml`** output for **coverage delta** pipelines.

## Layout

| Area | Role |
| --- | --- |
| `WhiteboxMetrix/Models/` | Entities (`User`, `Product`, `Order`, **v2** `Coupon`, loyalty fields) |
| `WhiteboxMetrix/Repositories/` | In-memory stores; **v2** `ICouponRepository` |
| `WhiteboxMetrix/Services/` | `UserService`, `ProductService`, `PricingService`, `OrderService`, `PaymentService`, **v2** `LoyaltyService` |
| `WhiteboxMetrix/Rules/` | `RuleEngine`, **v2** `BulkPricingAdjuster`, `LoyaltyCalculator` |
| `WhiteboxMetrix/Workflow/` | `OrderWorkflow` orchestration |
| `WhiteboxMetrix/Utils/` | Money/rounding helpers, risk, **data-flow noise** |
| `WhiteboxMetrix.Tests/` | NUnit tests (including **`GateCoverageTests`** for line/branch gates) |
| `Directory.Build.props` | **Stable Cobertura directory**: `TestResults/coverage/` |
| `.github/workflows/ci.yml` | **`dotnet test`** uploads `coverage.cobertura.xml` artifact |
| `.ci/coverage-baseline.json` | **Baseline metadata** template (`baseline_git_sha`, Cobertura path) for delta tooling |
| `stryker-config.json` | Stryker.NET configuration (solution + project name) |
| `.config/dotnet-tools.json` | **Local** tools: `dotnet-stryker`, ReportGenerator (coverage delta) |
| `CodeCoverage.runsettings` | **Optional** VSTest collector (primary path is **Coverlet MSBuild** on the test project) |
| `scripts/run-coverage.sh` | Save a **labeled** Cobertura snapshot under `artifacts/coverage/baselines/` |
| `scripts/compare-coverage.sh` | **Coverage delta** via ReportGenerator (`TextDelta` + HTML) |
| `scripts/run-mutation.sh` | Run Stryker using restored local tool |
| `benchmark/AllDefinitionsManifest.json` | **All-definition / def-use** seed list (manual or custom tooling) |

## Build and test (Coverlet / Cobertura — **non‑negotiable path**)

Every **`dotnet test`** run (Debug or Release) invokes **Coverlet.MSBuild** on `WhiteboxMetrix.Tests` and writes:

- `TestResults/coverage/coverage.cobertura.xml` — **Cobertura** (line **and** branch aggregates on the `<coverage>` root)
- `TestResults/coverage/coverage.json` — summary JSON

**CI / sandbox command** (same as GitHub Actions workflow):

```bash
dotnet restore WhiteboxMetrix.sln
dotnet test WhiteboxMetrix.sln -c Release
```

Optional **line/branch gates** are enforced via the test project MSBuild properties (`Threshold` / `ThresholdType`, currently **80% line**, **65% branch** totals — tune in `WhiteboxMetrix.Tests.csproj` if your org overrides thresholds).

Legacy one-off collection (VSTest collector only) — **does not** guarantee the fixed path above; prefer the default `dotnet test` flow:

```bash
dotnet test WhiteboxMetrix.sln -c Release --settings CodeCoverage.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Summarize a Cobertura file (rates + covered/valid counts):

```bash
python3 scripts/report_cobertura_summary.py TestResults/coverage/coverage.cobertura.xml
```

## Baseline for coverage **delta**

Downstream delta jobs need a **resolved baseline** (commit SHA + baseline Cobertura). This repo ships a template at **`.ci/coverage-baseline.json`**:

1. Run CI on `main` and download the **`coverage-cobertura`** artifact (or run `./scripts/run-coverage.sh my-baseline` locally).
2. Set **`baseline_git_sha`** in `.ci/coverage-baseline.json` to that **full** commit SHA.
3. Optionally paste **`last_line_rate`** / **`last_branch_rate`** from `report_cobertura_summary.py` after a green build.

Without that metadata, many products **skip** delta rows or show empty regression payloads — fixing only `coverage.cobertura.xml` is not enough if the worker never resolves `baseline_git_sha` / baseline artifact.

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

**v2** paths (coupons, bulk, loyalty) are exercised heavily in **`GateCoverageTests`** so CI gates stay green; subtle logic and rounding remain fair game for **Stryker** and static tools.

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

Tests include **many exact tier/coupon/discount expectations** in `GateCoverageTests`, but **end-to-end money** (tax + fudge + rounding stacks) is still non-obvious — useful for mutation tooling.

## Coverage gaps (residual)

| Zone | Notes |
| --- | --- |
| `OrderWorkflow.OrchestrateStandardPurchase` → **`price_failed`** | Only if `TryPriceOrder` fails (e.g. order removed from the in-memory repo between draft and pricing). Normal flow always prices the freshly created order. |

## Data-flow / definition issues

- `PricingAuditBuffer` fields — still useful **sinks** for def-use tooling even when not asserted in every test.
- `MoneyUtils.RoundingJitter` — **unused buffer** / overwrite pattern (intentional noise).
- `DataFlowNoise.Fuse` — multiple branches exercised in **`GateCoverageTests`**; still rich for all-definition tooling.

## Tooling goals (expected outcomes)

| Tool / metric | Expected signal |
| --- | --- |
| **Stryker** | Survivors remain in **thresholds**, **rounding**, and **nested conditionals** despite higher line coverage. |
| **Coverage delta** | Requires **baseline SHA + Cobertura** (see `.ci/coverage-baseline.json`); compare current `TestResults/coverage/coverage.cobertura.xml` to the baseline artifact. |
| **Data-flow / all-def** | Unused or rarely used definitions (`Scratch*`, noise locals, audit fields) stand out. |
| **Test quality** | Suite looks reasonable (orchestration + services) but **assertions are weak** (non-null, “≥ 0”, not exact behavior). |

## License

Provided as a **benchmark fixture**; use and modify freely for research and tooling evaluation.
