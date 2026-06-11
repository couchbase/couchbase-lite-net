# LiteCore native binaries ship in per-platform Support packages, not the main package

The `Couchbase.Lite` NuGet package is managed-only. The native LiteCore binaries — which are large and exist for every platform/architecture (Android ABIs, Apple xcframework, Windows x64/arm64, Linux, macOS) — are split into per-platform **Support** packages (`Couchbase.Lite.Support.{Android|Apple|NetDesktop|WinUI}`), each bundling only its platform's natives under `runtimes/<rid>/native/`. The main package depends on the relevant Support package per target framework, so a consumer downloads only the native binaries for the platform they actually build for.

## Considered Options

- **One monolithic package** containing all platforms' native binaries. Simplest to publish and reference, but every consumer — even a Windows-only desktop app — downloads every platform's LiteCore, making the package enormous. Rejected.
- **Per-platform Support packages** (chosen). Keeps the main package small and lets NuGet's per-TFM dependency resolution pull only the relevant natives.

## Consequences

- There are several packages to version and publish in lockstep rather than one.
- The Enterprise Edition build reuses these same `Couchbase.Lite.Support.*` projects but republishes them under `Couchbase.Lite.Enterprise.Support.*` names (and pins dependency versions exactly). That repackaging logic lives in the EE repo and is documented there.
