# AI Repository Guide — epi-bic-zoomroom

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


## Purpose

This repository is reviewed as **Zoom Room device plug-in variant**. This guide explains how an AI agent should inspect and change its source without relying on undocumented assumptions.

| Property | Value |
|---|---|
| Repository visibility | `public` |
| Documentation scope | Source-grounded repository guide plus one generated reference per declared C# or TypeScript type |
| Canonical class index | [CLASS_INDEX.md](CLASS_INDEX.md) |
| Generated type references | [types/](types/) |
| Generated on | `2026-08-12` |

## Read order for an AI agent

First, read the project file, package manifest, solution/workflow files, existing README, and the specific source file being changed. Next, read the generated type reference for the declaration and search for all usages. Finally, inspect tests and configuration examples before proposing a modification.

## Change rules

Preserve public type names, registered device type names, serialized property names, and externally consumed payload fields unless a deliberate compatibility change is documented and all consumers are updated. Treat factory classes as construction boundaries, configuration classes as wire/schema boundaries, and message types as transport boundaries. Verify package and framework versions from source rather than memory.

## Documentation maintenance

These files are generated from tracked declarations. Regenerate or update the relevant type reference whenever a type is added, removed, renamed, changes inheritance, changes an exposed member, or moves source file. Do not place credentials, deployment addresses, access tokens, or raw configuration values in generated documentation.
