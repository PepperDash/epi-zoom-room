# ControllerAbstractionTests

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


| Field | Source-grounded value |
|---|---|
| Repository | `epi-bic-zoomroom` |
| Source file | [`tests/ControllerAbstractionTests.cs`](../../../tests/ControllerAbstractionTests.cs) |
| Language | C# |
| Declaration | `class ControllerAbstractionTests` |
| Accessibility | `public` |
| Namespace/module | `PepperDash.Essentials.Plugins.Zoom.Room.Tests` |

## What

`ControllerAbstractionTests` is a test-support or verification type that protects a defined behavior from regression. This description is grounded in its source declaration and declared inheritance rather than inferred product behavior.

## Why

The type exists to provide a named boundary in the codebase. Its inheritance, implemented contracts, and public members define what surrounding code may rely on. Preserve that boundary unless a deliberate repository-wide compatibility change is intended.

## How it works

Public methods declared in this source file include: `IZoomRoomController_Exists_In_Assembly`, `IZoomRoomController_Is_Interface`, `IZoomRoomController_Extends_IDisposable`, `IZoomRoomController_Has_Lifecycle_Method`, `IZoomRoomController_Has_Meeting_Method`, `IZoomRoomController_Has_Audio_Method`, `IZoomRoomController_Has_Video_Method`, `IZoomRoomController_Has_Layout_Method`, `IZoomRoomController_Has_Share_Method`, `IZoomRoomController_Has_Recording_Method`, `IZoomRoomController_Has_Participant_Method`, `IZoomRoomController_Has_WaitingRoom_Method`. Use repository search to identify callers, implementers, serializers, tests, and configuration references before changing a public name or shape.

## When to modify it

Edit when the protected behavior changes intentionally or the test environment changes. Keep tests independent of unavailable platform services where possible.

## AI-agent change protocol

Before proposing a change, read this declaration, its full source file, all repository references to `ControllerAbstractionTests`, and its test coverage. Do not invent configuration keys, payload fields, interface members, or lifecycle ordering. Report the affected source files, tests, and consumer boundaries with any proposed change.

## Source authority

The source file linked above is authoritative. This generated reference is an index and decision aid; update it after a declaration, inheritance list, or public member contract changes.
