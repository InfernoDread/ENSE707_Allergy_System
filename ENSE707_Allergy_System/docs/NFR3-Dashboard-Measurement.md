# NFR3 Dashboard Measurement

## Harness

`QualityDashboardTests.OnGet_WithThreeHundredOrders_CompletesWithinFiveSeconds` creates fresh `InMemoryOrderStore` and `InMemoryAuditStore` instances for each test. It seeds 300 orders across all current statuses and 300 audit entries, then times `QualityDashboardModel.OnGet`. The fixture is test-only and does not use or modify the application's singleton demo stores.

Run it from the repository root:

```powershell
dotnet test 'ENSE707_Allergy_System\AllergySystem.Tests\AllergySystem.Tests.csproj' --no-restore --filter FullyQualifiedName~OnGet_WithThreeHundredOrders --logger 'console;verbosity=detailed'
```

## Result

- Latest measurement (15 October 2026): 9.66 ms for `OnGet` with 300 orders and 300 audit entries.
- Criterion: no more than 5,000 ms.
- Result: pass.

The automated timing measures the dashboard PageModel's in-process data aggregation path only. It does not measure application startup, HTTP request/response or network latency, or browser rendering.

The fixture uses fresh, isolated order and audit stores; it does not modify the application's singleton demo data.

## Manual browser check

On 15 October 2026, signed in as Admin and loaded `/Admin/QualityDashboard` through the running application. The page rendered successfully with the order summary, menu catalogue statistics, allergen association statistics, and recent audit activity. Recent authentication records displayed the standardized actor role `Admin`.
