# Agent & Future Developer Instructions

Comprehensive guide for AI agents, new developers, and anyone working on the epi-bic-zoomroom codebase.

---

## Before You Start

### 1. Read These Documents First (In Order)
1. **[README.md](README.md)** (5 min) - Overview of all documentation
2. **[QUICK-START.md](QUICK-START.md)** (10 min) - Clone and build the project
3. **[ARCHITECTURE.md](ARCHITECTURE.md)** (15 min) - Understand the system design
4. **[FORK-STRATEGY.md](FORK-STRATEGY.md)** (10 min) - How this fork is maintained
5. **THIS DOCUMENT** (20 min) - Common tasks and gotchas

### 2. Essential Context

**What is this repository?**
- It's a fork of PepperDash's Zoom Room plugin
- Maintained by Pepperdash Beincourt for the Beincourt courtroom AV system
- Main customization: Uses `StartSharingOnlyMeeting()` instead of `ShareBlackMagic()`

**Key Branches**:
- `csv-zoom-sandbox` - Main Beincourt branch (what you'll work on)
- `main` - Synced with upstream (read-only reference)
- Feature branches - Temporary, for development

**Deployment Model**:
- Push to `csv-zoom-sandbox` → GitHub Actions builds → Creates `.cplz` artifact → Published to GitHub Release → Deploy via PD Tools to CP4N

---

## Task 1: I Just Cloned the Repo, What Now?

1. **Verify setup** (follow [QUICK-START.md](QUICK-START.md)):
   ```bash
   cd epi-bic-zoomroom
   dotnet build -c Release
   # Should succeed with no errors
   ```

2. **Explore the structure**:
   - Read [DEVELOPMENT.md](DEVELOPMENT.md) → "Code Structure" section
   - Open `src/ZoomRoom.cs` - the main device class
   - Skim `src/ZoomRoomJoinMap.cs` - the SIMPL bridge

3. **Understand the fork**:
   - Read the [BEINCOURT.md](../BEINCOURT.md) file in the repo root
   - Look at the StartSharingOnlyMeeting change in `src/ZoomRoom.cs` (around line 1251)

4. **Check the current state**:
   ```bash
   git log --oneline -5  # See recent commits
   git branch -a         # See all branches
   ```

---

## Task 2: I Need to Make a Code Change

### Step-by-Step Guide

**Step 1: Create a feature branch**
```bash
git checkout -b feature/YOUR-FEATURE-NAME csv-zoom-sandbox
```

**Step 2: Make your changes**
- Edit the necessary files in `src/`
- Follow coding standards in [DEVELOPMENT.md](DEVELOPMENT.md)
- Add tests in `tests/` if adding new functionality

**Step 3: Build and test locally**
```bash
# Clean build
dotnet clean
dotnet build -c Release

# Run tests
dotnet test -c Release

# Should see: "Test Run Successful"
```

**Step 4: Create a good commit**
```bash
# See "Commit Message Format" below
git add .
git commit -m "feat: description of what you did

Detailed explanation of why this change was needed and what it fixes."
```

**Step 5: Push and create a PR**
```bash
git push -u origin feature/YOUR-FEATURE-NAME
# Then create PR on GitHub
```

**Step 6: Address review feedback**
```bash
# Make changes based on feedback
git add .
git commit -m "review: address feedback on PR #123"
git push
```

**Step 7: Merge**
- Once approved and tests pass, maintainer will merge
- Feature branch will be deleted

### Commit Message Format

**Good Example**:
```
feat: add support for screen blanking during presentations

Implements SetDisplayBlank and SetDisplayUnblank methods that allow
the SIMPL bridge to control video output. This is useful for hiding
the Zoom Room UI during presentations.

- Add display blank/unblank methods to ZoomRoom class
- Integrate with join map (digital joins #65-66)
- Add test cases for display control

Relates to CSS-1234
```

**Bad Examples**:
```
✗ fixed some bugs
✗ updates
✗ WIP
✗ asdf
```

**Format**:
- First line: Type + description (50 chars max)
- Blank line
- Body: Why and what (75 chars per line)
- Blank line
- Footer: Issue references

**Types**:
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation only
- `refactor:` - Code cleanup (no behavior change)
- `test:` - Test additions
- `chore:` - Build/config changes

---

## Task 3: There's a Build Error - How Do I Fix It?

### Common Build Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `error: PepperDash.ZoomRoom.Sdk could not be resolved` | NuGet auth not configured | See [QUICK-START.md](QUICK-START.md) "Step 3: GitHub Authentication" |
| `error: The project file could not be loaded` | .NET 8 SDK missing | Install .NET 8 from dotnet.microsoft.com |
| `error CS0234: The type or namespace 'X' does not exist` | Missing using statement | Add `using PepperDash.Essentials;` etc. |
| `error: Cannot find source 'github'` | NuGet source misconfigured | Run `dotnet nuget update source github --username USER --password PAT` |

### Debug Steps

1. **Clean and rebuild**:
   ```bash
   dotnet clean
   dotnet build -c Release -v d
   ```

2. **Check NuGet sources**:
   ```bash
   dotnet nuget list source
   # Should show: https://nuget.pkg.github.com/pepperdash/index.json
   ```

3. **Verify .NET version**:
   ```bash
   dotnet --version
   # Should be 8.0.100 or higher
   ```

4. **Check file encoding**:
   - Some files may have encoding issues
   - Ensure files are UTF-8 (no BOM)

5. **See [TROUBLESHOOTING.md](TROUBLESHOOTING.md)** for more help

---

## Task 4: A Test Failed - How Do I Debug?

### Running Tests with Diagnostics

```bash
# Run all tests with verbose output
dotnet test -c Release -v d

# Run specific test
dotnet test -c Release --filter "ControllerAbstractionTests"

# Run specific test method
dotnet test -c Release --filter "ZoomRoom_ConnectionStatus_ShouldBeDisconnectedOnInit"
```

### Understanding Test Failures

**Example Failure**:
```
FAILED ZoomRoomTests.ZoomRoom_ConnectionStatus_ShouldBeDisconnectedOnInit
Expected: Disconnected
Actual:   Connected
```

**Debugging approach**:
1. Check if the test reflects actual behavior
2. Check if the code change affected initialization
3. Review test setup (Arrange section)
4. Add Debug.LogInfo() to see execution flow
5. Run test again with verbose output

### Writing New Tests

See [DEVELOPMENT.md](DEVELOPMENT.md) → "Testing" section for examples.

---

## Task 5: I'm Cherry-Picking an Upstream Fix

This is a common task for maintaining the fork!

### Step-by-Step

1. **Find the fix commit**:
   ```bash
   git fetch upstream
   git log upstream/main --oneline | grep "your fix description"
   # Note the commit hash (e.g., abc1234)
   ```

2. **Cherry-pick into csv-zoom-sandbox**:
   ```bash
   git checkout csv-zoom-sandbox
   git cherry-pick abc1234
   ```

3. **If there's a conflict**:
   ```bash
   # Git will tell you which files conflict
   # Edit the conflicted files
   # Keep the Beincourt customization (StartSharingOnlyMeeting)
   # Resolve the conflict by hand
   
   git add .
   git cherry-pick --continue
   ```

4. **Test the cherry-pick**:
   ```bash
   dotnet build -c Release
   dotnet test -c Release
   ```

5. **Push and create PR**:
   ```bash
   git push -u origin csv-zoom-sandbox
   # Create PR on GitHub
   ```

**Important**: When cherry-picking, always verify that the Beincourt customization (`StartSharingOnlyMeeting`) is preserved. If the upstream fix touches that line, manual conflict resolution is required.

---

## Task 6: How Do I Deploy to CP4N Hardware?

You typically won't do this directly - it's automated via GitHub. But here's the process:

1. **Push to csv-zoom-sandbox**
   ```bash
   git push origin feature/my-feature
   # Create and merge PR
   ```

2. **GitHub Actions runs automatically**:
   - Builds the C# project → generates `.cplz` file
   - Runs tests
   - Creates GitHub Release with `.cplz` as asset

3. **In PD Tools**:
   - Select the CP4N processor
   - Go to Plugins
   - Check for epi-bic-zoomroom updates
   - Download and deploy the new version

4. **Hardware updates**:
   - CP4N reboots (or plugins reload)
   - New version is loaded

### Manual Deployment (Advanced)

If you need to deploy without using GitHub Actions:

1. **Build locally**:
   ```bash
   dotnet build -c Release
   ```

2. **Find the .cplz file**:
   ```
   src/bin/Release/epi-zoom-room.4Series.[version].cplz
   ```

3. **In PD Tools**:
   - Load the .cplz file manually
   - Deploy to CP4N

4. **In CP4N console**:
   - Restart plugin via `PluginReload` or restart processor

---

## Task 7: Understanding the SIMPL Join Map

The join map is how the plugin communicates with SIMPL+. Key things to know:

### Digital Joins (On/Off Buttons)

```csharp
// Device to SIMPL+ (feedback)
AddBoolFeedback(deviceToSimpl, "Microphone Muted", 15,
    () => device.MicrophonePrivacy);
// When device.MicrophonePrivacy changes, digital join #15 updates

// SIMPL+ to Device (commands)
AddAction(digitalToDevice, "Mute Microphone", 25,
    (device, state) =>
    {
        if (state.BoolValue)
            device.SetMicrophonePrivacy(true);
    });
// When SIMPL+ sends digital join #25, SetMicrophonePrivacy is called
```

### Analog Joins (Faders/Values)

```csharp
// Device to SIMPL+ (feedback)
AddAnalogFeedback(deviceToSimpl, "Volume Level", 200,
    () => (ushort)device.Volume);
// When device.Volume changes, analog join #200 updates (0-65535)

// SIMPL+ to Device (commands)
AddAction(analogToDevice, "Set Volume", 210,
    (device, state) =>
    {
        device.SetVolume((int)(state.UShortValue / 656)); // Scale to 0-100
    });
// When SIMPL+ sends analog value to join #210, SetVolume is called
```

### Serial Joins (Text)

```csharp
// Device to SIMPL+ (feedback)
AddStringFeedback(deviceToSimpl, "Device Name", 300,
    () => device.DeviceName);
// When device.DeviceName changes, serial join #300 updates

// SIMPL+ to Device (commands)
AddAction(serialToDevice, "Dial Number", 310,
    (device, state) =>
    {
        device.DialByNumber(state.StringValue);
    });
// When SIMPL+ sends serial text to join #310, DialByNumber is called
```

**Finding Available Join Numbers**:
- Search `ZoomRoomJoinMap.cs` for the highest number already used
- Avoid conflicts with existing joins
- Document new joins in comments

---

## Task 8: Understanding the Beincourt Customization

**The Key Difference**:

**Upstream (PepperDash)**:
```csharp
public override void StartSharing()
{
    _controller.ShareBlackMagic(true, true);  // Local emphasis
}
```

**Beincourt (This Fork)**:
```csharp
public override void StartSharing()
{
    StartSharingOnlyMeeting();  // Far-end emphasis
}
```

**Why This Matters**:
- `ShareBlackMagic()` - Focuses on local display, HDMI cable sharing
- `StartSharingOnlyMeeting()` - Focuses on sharing with meeting participants (remote observers)
- Beincourt's use case: courtroom judges/lawyers need to share evidence with remote participants

**When Working with Sharing**:
- ✅ Use `StartSharingOnlyMeeting()` for Beincourt
- ✅ Preserve this customization in all cherry-picks
- ✅ Test that remote participants can see shared content
- ❌ Don't change it back to `ShareBlackMagic()` without approval

---

## Important Gotchas

### Gotcha 1: This is a Fork - Don't Break the Customization

**Problem**: Cherry-picking an upstream fix that touches the `StartSharing()` method

**Solution**:
```csharp
// After cherry-pick, verify:
public override void StartSharing()
{
    StartSharingOnlyMeeting();  // ← MUST be this, not ShareBlackMagic
}
```

### Gotcha 2: NuGet Packages Come from Private Feed

**Problem**: Build fails with "Could not resolve PepperDash.ZoomRoom.Sdk"

**Solution**: Check GitHub authentication is configured (see [QUICK-START.md](QUICK-START.md) Step 3)

### Gotcha 3: The .cplz File Is Not Created by You

**Problem**: "How do I generate the .cplz file?"

**Solution**: GitHub Actions generates it automatically when you push to `csv-zoom-sandbox`. You just build with `dotnet build`.

### Gotcha 4: Branch Protection Is Active

**Problem**: "Why can't I push directly to csv-zoom-sandbox?"

**Solution**: Branch protection requires PRs and passing tests. This is intentional - it ensures code quality.

### Gotcha 5: The Build Takes Time

**Problem**: "Why is the build so slow?"

**Solution**: First-time .NET and NuGet restore takes 1-2 minutes. Subsequent builds are faster.

---

## Common Tasks Reference

### Add a New Button to SIMPL

```csharp
// 1. Add method to ZoomRoom.cs
public void DoSomethingNew()
{
    Debug.LogInfo("Doing something new...");
    _controller.DoSomethingNew();
}

// 2. Add join to ZoomRoomJoinMap.cs
AddAction(digitalToDevice, "Do Something New", 99,
    (device, state) =>
    {
        if (state.BoolValue)
            device.DoSomethingNew();
    });

// 3. Test
dotnet build -c Release
```

### Add State Feedback to SIMPL

```csharp
// 1. Add property to ZoomRoomStatus.cs
public bool IsDoingSomething { get; set; }

// 2. Add feedback to ZoomRoomJoinMap.cs
AddBoolFeedback(deviceToSimpl, "Is Doing Something", 199,
    () => device.Status.IsDoingSomething);

// 3. Update status when event fires
private void OnSomethingHappened()
{
    Status.IsDoingSomething = true;
    // Feedback automatically updated
}
```

### Handle a New SDK Event

```csharp
// 1. Add handler in ZrcSdkController.cs
case "SomethingHappened":
    OnSomethingHappened(data);
    break;

// 2. Add event to ZoomRoom.cs
public event EventHandler SomethingHappened;

// 3. Fire event when needed
SomethingHappened?.Invoke(this, EventArgs.Empty);
```

---

## Useful Commands Cheat Sheet

```bash
# Clone (first time)
git clone https://github.com/pepperdash-beincourt/epi-bic-zoomroom.git

# Update (before starting work)
git fetch origin
git pull origin csv-zoom-sandbox

# Create feature branch
git checkout -b feature/my-feature csv-zoom-sandbox

# Build
dotnet build -c Release

# Test
dotnet test -c Release

# Commit
git commit -m "feat: description"

# Push
git push -u origin feature/my-feature

# Switch branches
git checkout csv-zoom-sandbox
git checkout feature/my-feature

# See history
git log --oneline -10
git log --graph --oneline --all

# Cancel changes (LOCAL ONLY!)
git checkout -- src/SomeFile.cs

# Discard all changes (DESTRUCTIVE!)
git reset --hard HEAD
```

---

## Getting Help

1. **"Where do I find X?"** → Check the [documentation index](README.md)
2. **"How do I do Y?"** → Check [GLOSSARY.md](GLOSSARY.md) or this document
3. **"Why is Z failing?"** → Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
4. **"What's the architecture?"** → Read [ARCHITECTURE.md](ARCHITECTURE.md)
5. **"Still stuck?"** → Contact Chris Vance or refer to [CONTRIBUTING.md](CONTRIBUTING.md#questions)

---

## Your First Contribution Checklist

- [ ] I've read [QUICK-START.md](QUICK-START.md) and my build succeeds
- [ ] I've read [FORK-STRATEGY.md](FORK-STRATEGY.md) and understand the customization
- [ ] I've read [DEVELOPMENT.md](DEVELOPMENT.md) for coding standards
- [ ] I've created a feature branch from `csv-zoom-sandbox`
- [ ] I've made my changes and tested locally (`dotnet test -c Release`)
- [ ] I've written a descriptive commit message (see format above)
- [ ] I've pushed my branch and created a PR on GitHub
- [ ] I've addressed any review feedback
- [ ] My PR has been approved and tests are passing

**You're ready to contribute!**

---

**Last Updated**: 2026-08-10
**For Agents**: Use this document as your primary reference. Read the other docs as needed for specific tasks.
