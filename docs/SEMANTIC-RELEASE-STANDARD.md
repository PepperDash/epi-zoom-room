# Semantic-Release Standard for epi-bic-zoomroom

**Date**: 2026-08-10  
**Status**: Active Standard  
**Branch**: main, csv-zoom-sandbox  
**Replaces**: PepperDash essentialsplugins-getversion.yml workflow (deprecated)

---

## Overview

**Semantic-release** is the authoritative version management and release system for this repository.

**Why?** The PepperDash `essentialsplugins-getversion.yml` workflow failed to detect versions on the `csv-zoom-sandbox` branch. Semantic-release works reliably, integrates with conventional commits, and automates the entire release lifecycle.

---

## How It Works

### 1. Conventional Commits (Required)

All commits must follow [Conventional Commits](https://www.conventionalcommits.org/) format:

```
type(scope): description

optional body

optional footer
Co-authored-by: Name <email>
```

**Release-triggering types:**
- `feat:` → **minor** bump (1.0.0 → 1.1.0)
- `fix:` → **patch** bump (1.0.0 → 1.0.1)
- `BREAKING CHANGE:` footer → **major** bump (1.0.0 → 2.0.0)

**Non-releasing types:**
- `chore:`, `docs:`, `refactor:`, `style:`, `test:` (housekeeping only)

### 2. Release Detection & Tagging

Semantic-release:
1. Analyzes commits since last tag
2. Determines version bump (major/minor/patch)
3. Calculates next version using semver
4. Generates CHANGELOG.md
5. Creates git tag (`v2.0.0-csv-zoom-sandbox.1`)
6. Creates GitHub release with notes

### 3. Version Scheme

**Format**: `v{major}.{minor}.{patch}-{branch}.{build}` (on feature branches)

**Examples:**
- `v1.0.0` — main branch release
- `v2.0.0-csv-zoom-sandbox.1` — csv-zoom-sandbox release
- `v2.0.1-csv-zoom-sandbox.2` — patch on csv-zoom-sandbox

---

## Local Usage

### Run Semantic-Release

```bash
# Ensure you're on the correct branch and up-to-date
git checkout csv-zoom-sandbox
git pull origin csv-zoom-sandbox

# Run semantic-release
npx semantic-release
```

### Output

```
[semantic-release] › ℹ  Analyzing 128 commits since v1.0.0
[semantic-release] › ℹ  The release type for the commit is major
[semantic-release] › ℹ  The next release version is 2.0.0-csv-zoom-sandbox.1
[semantic-release] › ✔  Created tag v2.0.0-csv-zoom-sandbox.1
[semantic-release] › ✔  Published GitHub release
```

---

## CI/CD Integration

The GitHub Actions workflow (`.github/workflows/semantic-release.yml`) automatically:

1. **Triggers on push** to main or csv-zoom-sandbox
2. **Runs semantic-release** to analyze commits
3. **Creates tag + GitHub release** if new version detected
4. **Build workflow picks up the tag** and builds the .cplz artifact

---

## Commit Examples

**Minor (New Feature)**
```
feat(zoom): add ShowShareInstruction/DismissShareInstruction

Expose SDK methods to control sharing instruction overlay mode.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

**Patch (Bug Fix)**
```
fix(zoom): derive host state from roster, not just HostChanged event

Ensures IsHost is accurate even when room is host from meeting start.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

**Major (Breaking Change)**
```
feat(api)!: rename CameraSelected event signature to generic type

BREAKING CHANGE: CameraSelected now passes CameraSelectedEventArgs<IHasCameraControls>
instead of the non-generic signature. Subscribers must update their event handlers.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

**No Release (Housekeeping)**
```
chore: update org references from beincourt-engineering to pepperdash-beincourt

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

---

## Troubleshooting

### Tag Already Exists

**Error**: `fatal: tag 'v2.0.0-csv-zoom-sandbox.1' already exists`

**Cause**: Manual tag conflicts with semantic-release.

**Fix:**
```bash
git tag -d v2.0.0-csv-zoom-sandbox.1
git push fork --delete v2.0.0-csv-zoom-sandbox.1
npx semantic-release
```

### No New Version Detected

**Cause**: No commits since last tag, or commits don't follow conventional format.

**Check:**
```bash
git log --oneline <lastTag>..HEAD
```

Commits must start with `feat:`, `fix:`, or contain `BREAKING CHANGE:` to trigger a release.

### Build Not Triggered

**Cause**: GitHub Actions workflow not set up or tag not detected.

**Fix:**
- Verify `.github/workflows/semantic-release.yml` exists
- Check repo Actions settings: "Workflow permissions" → "Read and write"
- Verify the build workflow triggers on `push: tags: ['v*']`

---

## References

- [Semantic-Release Documentation](https://semantic-release.gitbook.io/)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [SemVer Specification](https://semver.org/)

---

**Maintained by**: Pepperdash Beincourt  
**Last Updated**: 2026-08-10
