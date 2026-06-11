# AGENTS.md

This file provides guidance to coding agents (Claude Code, etc.) when working with code in this repository.

> This is the **Community Edition (CE)** open-source core. It is normally consumed as a submodule of the Enterprise Edition (EE) repo `couchbase-lite-net-ee`, which drives the build. See that repo's `AGENTS.md` for EE-specific and build-orchestration details.

## Repository Overview

Couchbase Lite for .NET — a lightweight embedded NoSQL database with built-in sync to Couchbase Server / Sync Gateway. This repo is the CE core; the EE library wraps it with enterprise features (P2P replication, advanced encryption, etc.).

## Building

CE has no standalone Cake build script — it is built either by the EE repo's Cake build (`./build.sh --community`) or directly:

```bash
# Build the solution
dotnet build src/Couchbase.Lite.sln

# Build a single project
dotnet build src/Couchbase.Lite/Couchbase.Lite.csproj
```

LiteCore native binaries are required at runtime and are fetched/prebuilt under `vendor/` (`vendor/prebuilt_core`). Managed P/Invoke bindings for LiteCore live in `src/LiteCore/`.

## Running Tests

```bash
# Console core tests (fastest)
dotnet test src/Couchbase.Lite.Tests.NetCore/Couchbase.Lite.Tests.NetCore.csproj

# Run a single test
dotnet test src/Couchbase.Lite.Tests.NetCore/Couchbase.Lite.Tests.NetCore.csproj --filter "FullyQualifiedName~TestMethodName"
```

Test source shared across platforms lives in `src/Couchbase.Lite.Tests.Shared` (`.shproj`).

## Architecture

```
couchbase-lite-net/
├── src/
│   ├── Couchbase.Lite.sln
│   ├── Couchbase.Lite/                  # Main managed library
│   ├── Couchbase.Lite.Shared/           # Shared code across platforms (.shproj)
│   ├── Couchbase.Lite.Analyzer/         # Roslyn analyzers
│   ├── LiteCore/                        # LiteCore P/Invoke bindings (LiteCore.Shared.shproj)
│   ├── Couchbase.Lite.Support.Android/  # Platform support
│   ├── Couchbase.Lite.Support.Apple/
│   ├── Couchbase.Lite.Support.NetDesktop/
│   ├── Couchbase.Lite.Support.WinUI/
│   ├── Couchbase.Lite.Tests.NetCore/    # Console tests
│   ├── Couchbase.Lite.Tests.Shared/     # Shared test source (.shproj)
│   └── TargetFrameworks.props           # Single source of truth for .NET versions
├── Notes/
│   ├── StyleGuidelines.md               # Code style (see below)
│   └── CIAndVersions.md                 # Version-number scheme
├── packaging/                           # NuGet packaging config
├── Tools/
└── vendor/                              # LiteCore native (prebuilt_core)
```

`src/TargetFrameworks.props` is the single source of truth for .NET versions (MAUI, console, .NET Framework, Android minimum API).

**Dual-edition source.** `Couchbase.Lite.Shared` is compiled into *two* products: the Community Edition here, and the Enterprise Edition in the separate `couchbase-lite-net-ee` repo. Code gated behind `#if COUCHBASE_ENTERPRISE` compiles **only** in the EE build (which defines that symbol) — it is inert in CE builds. When editing shared source, check for these regions: EE-only API lives here even though it never compiles as part of CE.

## Code Style

From `Notes/StyleGuidelines.md`:
- 4 spaces, no tabs
- Private fields: `_camelCase`; private static: `_PascalCase`
- Properties, constants, static readonly: `PascalCase`
- All public non-changing fields should be `static readonly`, not `const`
- Bracket style: namespace/class/method/closures on new line; `if`/`else`/`while`/`for`/`using`/`try` inline
- Explicit `private` keyword always written (not implicit)
- Compiler aliases for primitives (`bool` not `Boolean`)
- XML doc comments (`///`) required on all public and protected methods
