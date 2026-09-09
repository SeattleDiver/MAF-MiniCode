# Development Instructions

Platform: .NET 10

Architecture:
- Five projects. Dependencies point one way: Cli to Agent to Tools to Workspace and Infrastructure.
- MiniCode.Workspace and MiniCode.Infrastructure reference nothing internal.

Rules:
- Use async APIs.
- Prefer a small record over a result hierarchy.
- One public type per file.
- Every path the model supplies goes through IWorkspace before anything opens a file.
- Do not add a NuGet package without saying why.
