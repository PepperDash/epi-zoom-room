# Beincourt Customizations for epi-bic-zoomroom

This is a fork of [PepperDash/epi-zoom-room](https://github.com/PepperDash/epi-zoom-room) maintained by [Pepperdash Beincourt](https://github.com/pepperdash-beincourt) for the Beincourt courtroom AV system (Pv2).

All Beincourt-specific customizations are isolated on the `csv-zoom-sandbox` branch to enable clean cherry-picking of upstream fixes while maintaining local enhancements.

---

## Beincourt Customizations

### 1. StartSharingOnlyMeeting API Change (csv-zoom-sandbox)

**Commit**: 4e3d1b1 "feat: update StartSharing method to use StartSharingOnlyMeeting for HDMI source sharing"

**Status**: ✅ Integrated in epi-beincourt-room; ready for hardware testing

**File**: `src/ZoomRoom.cs` (line 1251)

**Change**:
```csharp
// OLD (public repo)
public override void StartSharing() { _controller.ShareBlackMagic(true, true); }

// NEW (Beincourt)
public override void StartSharing() { StartSharingOnlyMeeting(); }
```

**Rationale**: 
- `ShareBlackMagic()` is intended for HDMI cable sharing with local display emphasis
- `StartSharingOnlyMeeting()` focuses on sharing content with meeting participants (far-end users)
- Beincourt courtroom uses Zoom Room for content sharing with remote participants, making the "sharing-only meeting" approach more appropriate for the use case

**Impact**:
- Zoom Room now launches a "sharing-only meeting" when starting content presentation
- Content is shared with far-end participants instead of just displaying locally
- Used by [epi-beincourt-room](https://github.com/pepperdash-beincourt/epi-beincourt-room) plugin during View All and Annotation presentation modes

---

## Branch Strategy

- **`upstream`** (read-only reference): https://github.com/PepperDash/epi-zoom-room.git
- **`fork`** (Beincourt fork): https://github.com/pepperdash-beincourt/epi-bic-zoomroom.git
- **`main`**: Synced with upstream/main (stable)
- **`feature/v3-migration`**: Upstream feature branch tracking
- **`csv-zoom-sandbox`**: **All Beincourt customizations live here** ← Branch protection enforced

### Cherry-Picking Upstream Fixes

When a fix is published in the public repo:

```bash
# 1. Fetch upstream
git fetch upstream

# 2. Identify fix commit
git log upstream/main --oneline | grep "your fix"

# 3. Cherry-pick into csv-zoom-sandbox
git checkout csv-zoom-sandbox
git cherry-pick <commit-hash>

# 4. Resolve conflicts (if any), test, push
git push fork csv-zoom-sandbox
```

All cherry-picks require user review and GitHub Actions passing before merging per branch protection rules.

---

## Testing & Deployment

### Local Build
```bash
# Requires GitHub Personal Access Token (PAT) configured for NuGet
dotnet build -c Release
```

### Output
- **Plugin**: `output/epi-bic-zoomroom.4Series.<version>.cplz`
- **Published via GitHub Actions** on each push to csv-zoom-sandbox (build must pass)
- **Package name**: `PepperDash.Essentials.Plugins.Bic.Zoom.Room`
- **Used by**: [epi-beincourt-room](https://github.com/pepperdash-beincourt/epi-beincourt-room)

---

## Dependencies

- **PepperDash.Essentials**: v3.0.0+ (dev v3-routing channel)
- **PepperDash.ZoomRoom.Sdk**: From upstream repo
- **.NET**: 8.0

---

## Maintaining This Fork

### For Beincourt Developers
1. All work goes to `csv-zoom-sandbox` branch
2. Create PR, ensure tests pass
3. Get user (Chris Vance) approval
4. Merge to csv-zoom-sandbox
5. GitHub Actions builds and publishes .cplz

### For Upstream Sync
- Monitor [PepperDash/epi-zoom-room/releases](https://github.com/PepperDash/epi-zoom-room/releases)
- Evaluate severity of fixes
- Cherry-pick applicable fixes to csv-zoom-sandbox
- User approval required before merge

### Conflict Resolution
If cherry-pick creates conflicts:
- Resolve manually, keeping StartSharingOnlyMeeting change
- Test thoroughly to ensure fix + Beincourt customization work together
- Document resolution in commit message

---

## Related Repositories

- **epi-beincourt-room**: Main Beincourt courtroom plugin, depends on this fork
- **PepperDash/epi-zoom-room**: Public upstream (source of truth for non-customized code)
- **pepperdash-beincourt/\***: Other Beincourt-specific forks following the same strategy

---

## Questions or Issues?

Contact: Chris Vance (Pepperdash Beincourt)

For upstream issues not related to Beincourt customizations, consider contributing to [PepperDash/epi-zoom-room](https://github.com/PepperDash/epi-zoom-room).
