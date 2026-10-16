# Development Instructions

Platform: .NET 10

Architecture:
- CustomerPortal.Core holds the domain: records and services, no I/O.
- CustomerPortal.Tests is an xunit.v3 project that tests Core.

Rules:
- Use async APIs for anything that does I/O.
- Customers are immutable records; change them with `with`, never by mutation.
- One public type per file.
- Every behaviour change comes with a test in CustomerPortal.Tests.
- Do not add a NuGet package without saying why.
