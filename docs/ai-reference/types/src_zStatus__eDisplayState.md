# eDisplayState

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


| Field | Source-grounded value |
|---|---|
| Repository | `epi-bic-zoomroom` |
| Source file | [`src/zStatus.cs`](../../../src/zStatus.cs) |
| Language | C# |
| Declaration | `enum eDisplayState` |
| Accessibility | `public` |
| Namespace/module | `PepperDash.Essentials.Plugins` |

## What

`eDisplayState` is a finite set of symbolic states or options used by the surrounding implementation. This description is grounded in its source declaration and declared inheritance rather than inferred product behavior.

## Why

The type exists to provide a named boundary in the codebase. Its inheritance, implemented contracts, and public members define what surrounding code may rely on. Preserve that boundary unless a deliberate repository-wide compatibility change is intended.

## How it works

Public methods declared in this source file include: `ConvertZoomContactsToGeneric`. Use repository search to identify callers, implementers, serializers, tests, and configuration references before changing a public name or shape.

## When to modify it

Edit when a finite state or option is intentionally added, renamed, or retired. Search all serializers, switches, and UI mappings first.

## AI-agent change protocol

Before proposing a change, read this declaration, its full source file, all repository references to `eDisplayState`, and its test coverage. Do not invent configuration keys, payload fields, interface members, or lifecycle ordering. Report the affected source files, tests, and consumer boundaries with any proposed change.

## Source authority

The source file linked above is authoritative. This generated reference is an index and decision aid; update it after a declaration, inheritance list, or public member contract changes.
