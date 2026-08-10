# Fork Maintenance Strategy

How to maintain this fork of PepperDash/epi-zoom-room while incorporating upstream fixes and keeping the Beincourt customization intact.

---

## Overview

This repository is a **strategic fork** of [PepperDash/epi-zoom-room](https://github.com/PepperDash/epi-zoom-room) with a single, essential customization for the Beincourt courtroom AV system.

**Goal**: Keep the fork stable, up-to-date with upstream, while protecting the Beincourt customization.

---

## Repository Structure

### Remote Repositories

```
upstream = https://github.com/PepperDash/epi-zoom-room.git
fork     = https://github.com/pepperdash-beincourt/epi-bic-zoomroom.git
```

### Local Remotes Configuration

```bash
git remote -v
# Should show:
# origin   https://github.com/pepperdash-beincourt/epi-bic-zoomroom.git (fetch)
# origin   https://github.com/pepperdash-beincourt/epi-bic-zoomroom.git (push)
# upstream https://github.com/PepperDash/epi-zoom-room.git (fetch)
```

If upstream is not configured:
```bash
git remote add upstream https://github.com/PepperDash/epi-zoom-room.git
git fetch upstream
```

---

## Branch Strategy

```
upstream/main
    │
    ├─ Public releases (read-only reference)
    │
    ▼
fork/main
    │
    ├─ Synced with upstream/main (stable branch)
    ├─ Used as base for stable releases
    │
    ▼
fork/csv-zoom-sandbox ← **BEINCOURT WORK HAPPENS HERE**
    │
    ├─ Contains all Beincourt customizations
    ├─ Cherry-picked upstream fixes
    ├─ Branch protection: PR + review + tests required
    ├─ Semantic-release publishes from this branch
    │
    ▼
fork/feature/* (temporary)
    │
    ├─ Feature branches for development
    ├─ Deleted after merge
```

### Branch Purposes

| Branch | Purpose | Protection | Who Can Push |
|--------|---------|-----------|-------------|
| `main` | Stable, synced with upstream | ✅ Yes | Maintainer only |
| `csv-zoom-sandbox` | **Beincourt customizations** | ✅ Yes | PRs only |
| `feature/*` | Feature development | ❌ No | Developer |
| Other feature branches | Experimental work | ❌ No | Developer |

---

## The Beincourt Customization

### What Changed

**File**: `src/ZoomRoom.cs`
**Location**: Line ~1251 (may vary)
**Method**: `StartSharing()`

### Original (Upstream)

```csharp
public override void StartSharing()
{
    _controller.ShareBlackMagic(true, true);
}
```

### Modified (Beincourt)

```csharp
public override void StartSharing()
{
    StartSharingOnlyMeeting();
}
```

### Why This Matters

- **ShareBlackMagic()**: Designed for HDMI cable sharing, emphasizes local display
- **StartSharingOnlyMeeting()**: Designed for sharing with meeting participants, emphasizes far-end users
- **Beincourt Use Case**: Courtroom needs to share evidence with remote participants

### When This Affects You

- ✅ When adding sharing features
- ✅ When cherry-picking upstream fixes that touch sharing
- ✅ When testing presentation/annotation features
- ❌ Don't change it without explicit approval

---

## Working with Upstream

### Scenario 1: A Critical Upstream Fix Is Released

**Example**: "Fix camera control crash"

**Steps**:

1. **Fetch upstream**
   ```bash
   git fetch upstream
   ```

2. **Review the fix**
   ```bash
   # Check the commit
   git log upstream/main --oneline | head -20
   # Find: "fix: camera control crash" (commit abc1234)
   
   # Review the change
   git show abc1234
   ```

3. **Determine if it affects Beincourt**
   ```bash
   # Does it touch StartSharing? Check the diff
   git show abc1234 -- src/ZoomRoom.cs
   # If it doesn't touch StartSharing or sharing-related code, it's safe
   ```

4. **Cherry-pick into csv-zoom-sandbox**
   ```bash
   git checkout csv-zoom-sandbox
   git cherry-pick abc1234
   ```

5. **If there's a conflict**
   ```bash
   # Git will halt with conflict markers
   # Edit the conflicted files (usually in src/ZoomRoom.cs)
   # Ensure StartSharingOnlyMeeting() is preserved
   # Remove conflict markers
   
   git add .
   git cherry-pick --continue
   ```

6. **Test the cherry-pick**
   ```bash
   dotnet build -c Release
   dotnet test -c Release
   ```

7. **Push and create PR**
   ```bash
   git push -u origin csv-zoom-sandbox
   # Create PR on GitHub for review
   ```

8. **After merge**
   - GitHub Actions will build and release
   - Version number will increment (patch version)
   - Deployed via PD Tools to CP4N

### Scenario 2: A Feature Upstream Requires Rebasing

**Example**: Major upstream refactor (e.g., .NET version upgrade)

**Steps**:

1. **Fetch upstream**
   ```bash
   git fetch upstream
   ```

2. **Check upstream's branch**
   ```bash
   git log upstream/main --oneline | head -20
   ```

3. **Rebase csv-zoom-sandbox onto upstream/main**
   ```bash
   git checkout csv-zoom-sandbox
   git rebase upstream/main
   ```

4. **Resolve conflicts** (if any)
   ```bash
   # Git will stop at each conflict
   # Edit the file to resolve
   # Keep Beincourt customization
   
   git add .
   git rebase --continue
   ```

5. **Verify the result**
   ```bash
   dotnet build -c Release
   dotnet test -c Release
   
   # Verify StartSharingOnlyMeeting() is still there
   grep -n "StartSharingOnlyMeeting" src/ZoomRoom.cs
   ```

6. **Force push** (only if you're authorized)
   ```bash
   git push --force origin csv-zoom-sandbox
   ```

### Scenario 3: Sync `main` Branch with Upstream

**Maintenance task**: Keep fork/main in sync with upstream/main

**Steps**:

1. **Fetch upstream**
   ```bash
   git fetch upstream
   ```

2. **Switch to main**
   ```bash
   git checkout main
   ```

3. **Merge upstream/main**
   ```bash
   git merge upstream/main --ff-only
   # --ff-only ensures we're just pulling in their changes, no new merges
   ```

4. **Push to fork**
   ```bash
   git push origin main
   ```

---

## Common Cherry-Pick Scenarios

### Good Candidate for Cherry-Pick

✅ Bug fixes (especially critical ones)
✅ Security updates
✅ Performance improvements
✅ SDK version updates (if compatible)
✅ Dependency updates
✅ Documentation improvements

### Risky Cherry-Picks

⚠️ Upstream changes that touch `StartSharing()` method
⚠️ Major refactoring
⚠️ Changes to interface implementations
⚠️ Changes to dependency versions without testing

### Don't Cherry-Pick

❌ Features that conflict with Beincourt requirements
❌ Major architectural changes
❌ Changes to sharing behavior (unless verified safe)

---

## Conflict Resolution Strategy

When cherry-picking creates conflicts:

### Step 1: Understand the Conflict

```bash
git show HEAD:src/ZoomRoom.cs | grep -A5 "StartSharing"
# Shows our (Beincourt) version

git show upstream/main:src/ZoomRoom.cs | grep -A5 "StartSharing"
# Shows their (upstream) version
```

### Step 2: Resolve Manually

Open the conflicted file and look for conflict markers:

```csharp
<<<<<<< HEAD (Beincourt - csv-zoom-sandbox)
public override void StartSharing()
{
    StartSharingOnlyMeeting();  // ← KEEP THIS
}
=======
public override void StartSharing()
{
    _controller.ShareBlackMagic(true, true);  // ← DISCARD
}
>>>>>>> upstream/main
```

**Resolution**:
```csharp
public override void StartSharing()
{
    StartSharingOnlyMeeting();  // Keep Beincourt customization
}
```

### Step 3: Verify and Continue

```bash
git add src/ZoomRoom.cs
git cherry-pick --continue
```

### Step 4: Test

```bash
dotnet build -c Release
dotnet test -c Release
```

### Step 5: Document in Commit Message

```bash
# After cherry-pick completes, amend the commit message if needed
git log --oneline -1  # See the commit

# Create a new commit if needed to document the resolution
git commit --allow-empty -m "chore: document conflict resolution for cherry-pick

Resolved conflict in StartSharing() method by preserving Beincourt's
StartSharingOnlyMeeting() customization. Upstream change was attempting
to use ShareBlackMagic() but our fork requires the far-end focus."
```

---

## Preventing Conflicts

### Best Practices

1. **Cherry-pick frequently**
   - Don't let upstream changes accumulate
   - Weekly check for critical fixes is ideal

2. **Keep the customization isolated**
   - The StartSharingOnlyMeeting change is in one method
   - Avoid touching other code in that class unnecessarily
   - This reduces merge conflicts

3. **Test cherry-picks before merging**
   - Always build and test locally
   - Run the full test suite
   - Verify sharing functionality works

4. **Use meaningful PR descriptions**
   - Document what upstream fix you're cherry-picking
   - Explain any conflicts resolved
   - Link to upstream issue if applicable

### Git Configuration

To reduce merge conflicts, configure git to handle certain files specially:

```bash
# .gitattributes (already in repo)
# Helps git understand how to merge certain file types
```

---

## Release Workflow

### Automatic Release (Preferred)

1. **Push to csv-zoom-sandbox**
   ```bash
   git push origin feature/my-feature
   # Then merge via PR
   ```

2. **GitHub Actions kicks in**:
   - Builds the project
   - Runs tests
   - Generates `.cplz` artifact
   - Publishes to GitHub Release

3. **Version bumped automatically** (semantic-release)
   - `feat:` commits → Minor version bump (v1.0.0 → v1.1.0)
   - `fix:` commits → Patch version bump (v1.0.0 → v1.0.1)
   - Commit messages determine versioning

### Manual Release (Emergency Only)

```bash
# This is NOT the normal process
# Only do this if GitHub Actions fails

# 1. Build locally
dotnet build -c Release

# 2. Find the .cplz file
ls src/bin/Release/*.cplz

# 3. Create a GitHub Release manually
# - Go to https://github.com/pepperdash-beincourt/epi-bic-zoomroom/releases
# - Click "New release"
# - Tag: v2.x.x (following semantic versioning)
# - Upload the .cplz file
```

---

## Maintenance Checklist

### Weekly
- [ ] Check upstream for critical security fixes
- [ ] Review new GitHub issues in this repo

### Monthly
- [ ] Review upstream releases
- [ ] Cherry-pick applicable fixes
- [ ] Run full test suite
- [ ] Update dependencies if needed

### Quarterly
- [ ] Plan major feature work
- [ ] Evaluate upstream v3 migration progress
- [ ] Document lessons learned

### Before Each Release
- [ ] All tests pass
- [ ] Build succeeds locally
- [ ] StartSharingOnlyMeeting() is preserved
- [ ] Commit messages follow convention
- [ ] Documentation is up-to-date

---

## Troubleshooting Fork Issues

### Problem: Cherry-pick has merge conflicts

**Solution**: See "Conflict Resolution Strategy" section above

### Problem: Can't push to csv-zoom-sandbox (branch protected)

**Expected behavior**: Branch protection requires PR and passing tests
**Solution**: Create a PR instead of pushing directly

### Problem: Upstream has a breaking change

**Solution**:
1. Create a feature branch from upstream/main
2. Evaluate if we need the change
3. If yes, create migration plan
4. If no, monitor for workarounds

### Problem: Lost commits after rebase

**Recovery**:
```bash
git reflog  # Shows all recent commits
git reset --hard abc1234  # Restore to commit
git push --force  # Be careful!
```

---

## Communication

### When to Notify Maintainers

- ✅ Critical upstream fixes available
- ✅ Major version upstream (v1.x → v2.x)
- ✅ Conflicts that can't be auto-resolved
- ✅ Breaking changes in dependencies
- ✅ Questions about fork strategy

### Upstream Contribution Process

If you find an upstream issue and want to fix it:

1. **Fix it locally in csv-zoom-sandbox**
   - Commit with clear message
   - Test thoroughly

2. **Create PR to upstream (PepperDash/epi-zoom-room)**
   - Fork the public repo
   - Apply your fix
   - Create PR with detailed description

3. **Once merged upstream**
   - Cherry-pick the fix into our fork
   - Ensure tests pass

---

## Related Documentation

- **[ARCHITECTURE.md](ARCHITECTURE.md)** - System design details
- **[DEVELOPMENT.md](DEVELOPMENT.md)** - Code standards and workflow
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - PR and commit guidelines
- **[AGENT-INSTRUCTIONS.md](AGENT-INSTRUCTIONS.md)** - Practical task guide
- **[../BEINCOURT.md](../BEINCOURT.md)** - Beincourt customization overview

---

**Last Updated**: 2026-08-10
**Fork Maintainer**: Chris Vance (Pepperdash Beincourt)
**Upstream**: https://github.com/PepperDash/epi-zoom-room
