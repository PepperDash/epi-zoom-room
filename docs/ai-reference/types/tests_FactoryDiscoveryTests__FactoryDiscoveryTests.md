# FactoryDiscoveryTests

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


| Field | Source-grounded value |
|---|---|
| Repository | `epi-bic-zoomroom` |
| Source file | [`tests/FactoryDiscoveryTests.cs`](../../../tests/FactoryDiscoveryTests.cs) |
| Language | C# |
| Declaration | `class FactoryDiscoveryTests` |
| Accessibility | `public` |
| Namespace/module | `PepperDash.Essentials.Plugins.Zoom.Room.Tests` |

## What

`FactoryDiscoveryTests` is a construction boundary that maps configuration or discovery inputs into concrete objects. This description is grounded in its source declaration and declared inheritance rather than inferred product behavior.

## Why

The type exists to provide a named boundary in the codebase. Its inheritance, implemented contracts, and public members define what surrounding code may rely on. Preserve that boundary unless a deliberate repository-wide compatibility change is intended.

## How it works

Public methods declared in this source file include: `Assembly_Loads_Successfully`, `Assembly_Name_Matches_Expected`, `Factory_Count_Matches_Expected`, `Factory_Exists_ByName`, `All_Factories_Have_Parameterless_Constructor`. Use repository search to identify callers, implementers, serializers, tests, and configuration references before changing a public name or shape.

## When to modify it

Edit when construction, type registration, or configuration validation changes. Confirm registration and configuration names from source before modifying.

## AI-agent change protocol

Before proposing a change, read this declaration, its full source file, all repository references to `FactoryDiscoveryTests`, and its test coverage. Do not invent configuration keys, payload fields, interface members, or lifecycle ordering. Report the affected source files, tests, and consumer boundaries with any proposed change.

## Source authority

The source file linked above is authoritative. This generated reference is an index and decision aid; update it after a declaration, inheritance list, or public member contract changes.
