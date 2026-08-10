# epi-bic-zoomroom Documentation

This directory contains comprehensive documentation for the Beincourt fork of the PepperDash Zoom Room Essentials plugin. Use this as your guide for understanding, developing, deploying, and maintaining this codebase.

## Documentation Index

### Getting Started
- **[QUICK-START.md](QUICK-START.md)** - Clone, setup, and your first build in 10 minutes
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - System design, fork strategy, component overview

### Development
- **[DEVELOPMENT.md](DEVELOPMENT.md)** - Code structure, conventions, workflow, and coding standards
- **[BUILDING.md](BUILDING.md)** - How to build the C# plugin locally
- **[CI-CD-PIPELINE.md](CI-CD-PIPELINE.md)** - GitHub Actions workflow, automation, release process

### Deployment & Operations
- **[DEPLOYMENT.md](DEPLOYMENT.md)** - Deploying to CP4N hardware, testing procedures, rollback
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Common issues, diagnostics, solutions

### Reference
- **[GLOSSARY.md](GLOSSARY.md)** - Terminology definitions (Essentials, Beincourt, Zoom Room SDK, etc.)
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - Contributing guidelines, PR process, commit conventions
- **[FORK-STRATEGY.md](FORK-STRATEGY.md)** - Maintaining this fork, cherry-picking upstream fixes, conflict resolution

### For AI Agents & Future Developers
- **[AGENT-INSTRUCTIONS.md](AGENT-INSTRUCTIONS.md)** - Comprehensive instructions for AI agents and future developers working on this codebase

---

## Quick Navigation

### I want to...

**...clone and build the project**
→ See [QUICK-START.md](QUICK-START.md)

**...understand how this fork works**
→ See [ARCHITECTURE.md](ARCHITECTURE.md) and [FORK-STRATEGY.md](FORK-STRATEGY.md)

**...make a code change**
→ See [DEVELOPMENT.md](DEVELOPMENT.md) and [CONTRIBUTING.md](CONTRIBUTING.md)

**...build locally for testing**
→ See [BUILDING.md](BUILDING.md)

**...deploy to CP4N hardware**
→ See [DEPLOYMENT.md](DEPLOYMENT.md)

**...understand the CI/CD pipeline**
→ See [CI-CD-PIPELINE.md](CI-CD-PIPELINE.md)

**...troubleshoot a build or runtime issue**
→ See [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

**...understand a term or concept**
→ See [GLOSSARY.md](GLOSSARY.md)

**...get started as a new developer/agent**
→ See [AGENT-INSTRUCTIONS.md](AGENT-INSTRUCTIONS.md)

---

## Repository Overview

**Repository**: `epi-bic-zoomroom`
**Owner**: Pepperdash Beincourt
**Language**: C# (.NET 8)
**Purpose**: Essentials plugin that provides Zoom Room control for the Beincourt courtroom AV system
**Upstream**: [PepperDash/epi-zoom-room](https://github.com/PepperDash/epi-zoom-room)

### Key Branches

| Branch | Purpose | Status |
|--------|---------|--------|
| `main` | Synced with upstream main | Stable |
| `csv-zoom-sandbox` | **Beincourt customizations live here** | Active |
| `csv-zoom-sandbox-v2` | Development branch (experimental features) | Development |
| `feature/*` | Feature branches | Temporary |

### Customizations

The main customization in this fork is a change to the `StartSharing()` method to use `StartSharingOnlyMeeting()` instead of `ShareBlackMagic()`, which is better suited for the Beincourt courtroom's use case of sharing content with remote meeting participants.

See [FORK-STRATEGY.md](FORK-STRATEGY.md) and [BEINCOURT.md](../BEINCOURT.md) for details.

---

## Key Files & Directories

```
epi-bic-zoomroom/
├── src/                    # C# source code
│   ├── ZoomRoom.cs        # Main plugin class
│   ├── ZoomRoomJoinMap.cs # SIMPL bridge join mappings
│   ├── zCommand.cs        # Command parsing
│   ├── zStatus.cs         # Status event handling
│   └── ...other files
├── tests/                  # C# test suite
├── .github/
│   ├── workflows/         # GitHub Actions CI/CD
│   └── scripts/           # PowerShell build scripts
├── docs/                  # This documentation (you are here)
├── .releaserc.json        # Semantic-release configuration
├── package.json           # npm configuration
├── nuget.config           # NuGet package sources
├── BEINCOURT.md           # Beincourt fork documentation
└── README.md              # Original PepperDash template readme
```

---

## Development Setup Checklist

- [ ] Clone the repository
- [ ] Install .NET 8 SDK
- [ ] Configure GitHub Personal Access Token (PAT) for NuGet
- [ ] Run `dotnet build -c Release` to verify setup
- [ ] Familiarize yourself with [QUICK-START.md](QUICK-START.md)
- [ ] Read [ARCHITECTURE.md](ARCHITECTURE.md) to understand the design
- [ ] Review [DEVELOPMENT.md](DEVELOPMENT.md) for coding conventions

---

## Important Links

- **GitHub Fork**: https://github.com/pepperdash-beincourt/epi-bic-zoomroom
- **Upstream Repo**: https://github.com/PepperDash/epi-zoom-room
- **Essentials Wiki**: https://github.com/PepperDash/Essentials/wiki
- **Zoom Room SDK Docs**: [See GLOSSARY.md](GLOSSARY.md#zoom-room-sdk)
- **PepperDash Docs**: https://pepperdash.github.io/

---

## Getting Help

1. **Search [TROUBLESHOOTING.md](TROUBLESHOOTING.md)** for known issues
2. **Check [GLOSSARY.md](GLOSSARY.md)** for terminology
3. **Review [AGENT-INSTRUCTIONS.md](AGENT-INSTRUCTIONS.md)** for common tasks
4. **Contact**: Chris Vance (Pepperdash Beincourt)

---

## Document Status

| Document | Last Updated | Status | Reviewer |
|----------|--------------|--------|----------|
| README.md | 2026-08-10 | ✅ Complete | - |
| QUICK-START.md | TBD | 🔄 In Progress | - |
| ARCHITECTURE.md | TBD | 🔄 In Progress | - |
| DEVELOPMENT.md | TBD | 🔄 In Progress | - |
| BUILDING.md | TBD | 🔄 In Progress | - |
| CI-CD-PIPELINE.md | TBD | 🔄 In Progress | - |
| DEPLOYMENT.md | TBD | 🔄 In Progress | - |
| TROUBLESHOOTING.md | TBD | 🔄 In Progress | - |
| GLOSSARY.md | TBD | 🔄 In Progress | - |
| CONTRIBUTING.md | TBD | 🔄 In Progress | - |
| FORK-STRATEGY.md | TBD | 🔄 In Progress | - |
| AGENT-INSTRUCTIONS.md | TBD | 🔄 In Progress | - |

---

**Last Updated**: 2026-08-10
**Documentation Version**: 1.0
