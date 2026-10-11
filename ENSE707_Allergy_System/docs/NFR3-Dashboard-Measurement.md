# NFR3 Dashboard Measurement

## Harness

`QualityDashboardTests.OnGet_WithThreeHundredOrders_CompletesWithinFiveSeconds` creates fresh `InMemoryOrderStore` and `InMemoryAuditStore` instances for each test. It seeds 300 orders across all current statuses and 300 audit entries, then times `QualityDashboardModel.OnGet`. The fixture is test-only and does not use or modify the application's singleton demo stores.

Run it from the repository root:

```powershell
dotnet test 'ENSE707_Allergy_System\AllergySystem.Tests\AllergySystem.Tests.csproj' --no-restore --filter FullyQualifiedName~OnGet_WithThreeHundredOrders --logger 'console;verbosity=detailed'
```

## Result

- Measurement: 14.11 ms for `OnGet` with 300 orders and 300 audit entries.
- Criterion: no more than 5,000 ms.
- Result: pass.

This measures the dashboard PageModel's in-process data aggregation, not application startup, HTTP transport, or browser rendering. The browser workflow was checked separately.