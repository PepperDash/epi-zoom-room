# ZoomRoomMessenger

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


| Field | Source-grounded value |
|---|---|
| Repository | `epi-bic-zoomroom` |
| Source file | [`src/MobileControlMessenger/ZoomRoomMessenger.cs`](../../../src/MobileControlMessenger/ZoomRoomMessenger.cs) |
| Language | C# |
| Declaration | `class ZoomRoomMessenger` with declared base/contract list `MessengerBase` |
| Accessibility | `public` |
| Namespace/module | `PepperDash.Essentials.AppServer.Messengers` |

## What

`ZoomRoomMessenger` is a message-handling boundary that translates between transport payloads and application behavior. This description is grounded in its source declaration and declared inheritance rather than inferred product behavior.

## Why

The type exists to provide a named boundary in the codebase. Its inheritance, implemented contracts, and public members define what surrounding code may rely on. Preserve that boundary unless a deliberate repository-wide compatibility change is intended.

## How it works

Preserve the declared inheritance/contract relationship: `MessengerBase`. Use repository search to identify callers, implementers, serializers, tests, and configuration references before changing a public name or shape.

## When to modify it

Edit when an action path, payload shape, status message, or subscription behavior changes. Treat it as a cross-boundary compatibility change.

## AI-agent change protocol

Before proposing a change, read this declaration, its full source file, all repository references to `ZoomRoomMessenger`, and its test coverage. Do not invent configuration keys, payload fields, interface members, or lifecycle ordering. Report the affected source files, tests, and consumer boundaries with any proposed change.

## Source authority

The source file linked above is authoritative. This generated reference is an index and decision aid; update it after a declaration, inheritance list, or public member contract changes.
