# Development Guide

Code structure, conventions, workflow, and standards for developing in epi-bic-zoomroom.

---

## Code Structure

```
src/
├── ZoomRoom.cs                 # Main device class (→ START HERE)
├── ZoomRoomJoinMap.cs          # SIMPL bridge join mappings
├── ZoomRoomPropertiesConfig.cs # Configuration object
├── ZoomRoomStatus.cs           # Status feedback object
├── ZoomRoomInfo.cs             # Device information object
├── ZoomRoomFactory.cs          # Plugin discovery & instantiation
├── ZoomRoomConfiguration.cs    # Configuration file parsing
├── zCommand.cs                 # Command parsing utility
├── zConfiguration.cs           # Configuration file reader
├── zEvent.cs                   # Event handling
├── zStatus.cs                  # Status feedback
├── Controller/
│   ├── IZoomRoomController.cs  # SDK interface
│   ├── ZrcSdkController.cs     # SDK implementation
│   └── SdkConnectionMonitor.cs # Connection tracking
├── MobileControlMessenger/     # Mobile Control integrations
│   ├── IHasCameraAutoModeMessenger.cs
│   ├── IHasCodecRoomPresetsActionsMessenger.cs
│   ├── IHasMeetingLockMessenger.cs
│   ├── IHasMeetingRecordingWithPromptMessenger.cs
│   ├── IHasParticipantPinUnpinMessenger.cs
│   ├── IHasParticipantsMessenger.cs
│   ├── IHasPhoneDialingMessenger.cs
│   ├── IHasPresentationOnlyMeetingMessenger.cs
│   ├── IHasSelfviewPositionMessenger.cs
│   ├── IHasSelfviewSizeMessenger.cs
│   ├── IHasZoomRoomLayoutsMessenger.cs
│   └── ZoomRoomMessenger.cs
├── Directory.Build.props       # Build configuration
└── Directory.Build.targets     # Build targets

tests/
├── ControllerAbstractionTests.cs
├── ConfigDeserializationTests.cs
├── FactoryDiscoveryTests.cs
├── FactoryMetadataTests.cs
└── AssemblyFixture.cs
```

### Key Files to Know

| File | Purpose | When to Edit |
|------|---------|-------------|
| `ZoomRoom.cs` | Main device logic | Adding features, fixing behavior |
| `ZoomRoomJoinMap.cs` | SIMPL integration | Adding new controls to join map |
| `ZrcSdkController.cs` | SDK communication | Changing how hardware is controlled |
| `zStatus.cs` | Status parsing | Adding new state feedback |
| `zCommand.cs` | Command handling | Parsing new command types |
| `MobileControlMessenger/*.cs` | Mobile Control | Adding mobile app features |

---

## Coding Standards & Conventions

### C# Style Guide

**Naming Conventions**:
```csharp
// Classes: PascalCase
public class ZoomRoomController { }

// Methods: PascalCase
public void ConnectToDevice() { }

// Properties: PascalCase
public string DeviceName { get; set; }

// Private fields: camelCase with underscore prefix
private string _activationCode;

// Constants: UPPER_CASE
private const int MAX_RETRIES = 3;

// Events: PascalCase ending in "Changed", "Fired", "Received"
public event EventHandler ConnectionStatusChanged;
```

**Formatting**:
```csharp
// Use 4 spaces for indentation (Visual Studio default)

// Prefer expression-bodied members for simple properties
public string Name => _name;

// Use null-coalescing and conditional operators
string value = input ?? "default";
bool condition = x > 0 ? true : false;

// Use string interpolation
string message = $"Device '{_name}' connected";

// Use async/await, not Task chains
public async Task ConnectAsync()
{
    await _controller.ConnectAsync();
}
```

**Comments & Documentation**:
```csharp
/// <summary>
/// Sends a command to the Zoom Room device.
/// </summary>
/// <param name="command">The command to send</param>
/// <returns>True if successful, false otherwise</returns>
/// <remarks>
/// This method will throw ZoomException if the device is not connected.
/// Retry logic is handled internally with exponential backoff.
/// </remarks>
public bool SendCommand(string command)
{
    // Implementation
}
```

**Error Handling**:
```csharp
// Use specific exception types
throw new ArgumentNullException(nameof(parameter), "Parameter cannot be null");

// Log errors before throwing
Debug.LogError($"Failed to connect: {ex.Message}");
throw;

// Use try-catch for recovery scenarios
try
{
    Connect();
}
catch (ConnectionException ex)
{
    Debug.LogWarn($"Connection failed, retrying: {ex.Message}");
    Reconnect();
}
```

---

## Common Development Tasks

### Task 1: Add a New Control (Button/Fader)

1. **Identify the device capability**
   - Check Zoom Room SDK documentation
   - Verify it's supported by the hardware version

2. **Add to ZoomRoom.cs**
   ```csharp
   /// <summary>
   /// Enables screen sharing for the specified source.
   /// </summary>
   public void StartSharingFromSource(string sourceType)
   {
       if (_controller == null)
           throw new InvalidOperationException("Device not connected");
       
       _controller.StartSharing(sourceType);
       Debug.LogInfo($"Started sharing from {sourceType}");
   }
   ```

3. **Add to ZoomRoomJoinMap.cs**
   ```csharp
   // In the digital join mappings
   AddAction(digitalToDevice, "Start Sharing HDMI", 
       (device, state) => 
       {
           if (state.BoolValue)
               device.StartSharingFromSource("HDMI");
       });
   
   // Add feedback for the join
   AddBoolFeedback(deviceToSimpl, "Sharing HDMI", 50,
       () => device.CurrentSharingSource == "HDMI");
   ```

4. **Test the join**
   - Verify the digital/analog join numbers don't conflict
   - Test the action fires the method
   - Test feedback updates correctly

5. **Update documentation**
   - Add to join map comments
   - Update [GLOSSARY.md](GLOSSARY.md) if introducing new concepts

### Task 2: Handle a New SDK Event

1. **Check ZrcSdkController.cs**
   ```csharp
   // Look for how other events are handled
   case "ParticipantCountChanged":
       OnParticipantCountChanged(data);
       break;
   ```

2. **Add handler in ZoomRoom.cs**
   ```csharp
   public event EventHandler<ParticipantEventArgs> OnParticipantListChanged;

   private void HandleParticipantListChanged(JToken data)
   {
       var participants = ParseParticipants(data);
       Debug.LogInfo($"Participant list updated: {participants.Count} participants");
       OnParticipantListChanged?.Invoke(this, 
           new ParticipantEventArgs { Participants = participants });
   }
   ```

3. **Update ZoomRoomStatus.cs**
   ```csharp
   public List<ParticipantInfo> Participants { get; set; }
   ```

4. **Add SIMPL feedback (optional)**
   ```csharp
   // In ZoomRoomJoinMap.cs
   AddStringFeedback(deviceToSimpl, "Participant List JSON", 300,
       () => JsonConvert.SerializeObject(device.Status.Participants));
   ```

### Task 3: Fix a Bug

1. **Identify the issue**
   - Review error logs
   - Check if it's reproducible
   - Determine affected component (SDK, Join Map, etc.)

2. **Create a test case (optional but recommended)**
   ```csharp
   [Fact]
   public void MuteUnmuteToggle_ShouldToggleMuteState()
   {
       // Arrange
       var device = new ZoomRoom();
       device.MicrophonePrivacy = false;
       
       // Act
       device.SetMicrophonePrivacy(true);
       
       // Assert
       Assert.True(device.MicrophonePrivacy);
   }
   ```

3. **Make the minimal fix**
   - Don't refactor unrelated code
   - Add comments explaining the fix
   - Reference the issue number if applicable

4. **Test the fix**
   ```bash
   dotnet test -c Release
   ```

5. **Create a commit**
   ```bash
   git commit -m "fix: microphone toggle not updating state

   The MicrophonePrivacy property was being set but feedback wasn't being
   sent to SIMPL. Added explicit feedback update call in SetMicrophonePrivacy.

   Fixes #123"
   ```

---

## Development Workflow

### 1. Start a Feature

```bash
# Update from remote
git fetch origin
git pull origin csv-zoom-sandbox

# Create feature branch
git checkout -b feature/my-feature csv-zoom-sandbox

# Verify you're on the right branch
git branch  # Should show * feature/my-feature
```

### 2. Make Changes

```bash
# Edit files in src/ and tests/

# Build to check for compilation errors
dotnet build -c Release

# Run tests
dotnet test -c Release
```

### 3. Commit Changes

**Good Commit Message Example**:
```
feat: add screen blanking control for presentation mode

Adds SetDisplayBlank and SetDisplayUnblank methods to control
video output during presentations. Integrated with SIMPL bridge
via digital joins #65-66.

- Add display blank methods to ZoomRoom class
- Add join map entries for digital #65 (blank) and #66 (unblank)
- Add feedback digital #165 (is blank)
- Add test cases in ControllerAbstractionTests

Relates to: CSS-1234
```

**Commit Format**:
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`
**Scope**: (optional) affected component, e.g., `(mobile-control)`, `(sdk)`
**Subject**: What changed (50 chars max)
**Body**: Why it changed (75 chars per line)
**Footer**: Issue references, breaking changes

### 4. Push & Create PR

```bash
# Push branch
git push -u origin feature/my-feature

# Create PR on GitHub
# - Use descriptive title
# - Link related issues
# - Explain what changed and why
```

### 5. Review & Merge

- Address review feedback
- Ensure all tests pass
- Get approval from code maintainer
- Merge to csv-zoom-sandbox
- Delete feature branch

---

## Testing

### Running Tests Locally

```bash
# Run all tests
dotnet test -c Release

# Run specific test file
dotnet test -c Release --filter "ControllerAbstractionTests"

# Run with verbose output
dotnet test -c Release -v d

# Run and generate coverage report
dotnet test -c Release /p:CollectCoverage=true
```

### Writing Tests

```csharp
[Fact]
public void ConnectionStatus_ShouldBeDisconnectedOnInit()
{
    // Arrange
    var device = new ZoomRoom();
    
    // Act
    var status = device.ConnectionStatus;
    
    // Assert
    Assert.Equal(ConnectionStatus.Disconnected, status);
}

[Theory]
[InlineData("HDMI")]
[InlineData("Content")]
[InlineData("AirPlay")]
public void StartSharing_ShouldSucceedForValidSources(string source)
{
    // Arrange
    var device = new ZoomRoom();
    device.Connect();
    
    // Act
    device.StartSharing(source);
    
    // Assert
    Assert.True(device.IsSharingContent);
}
```

---

## Debugging Tips

### Enable Debug Logging

In your configuration:
```json
{
  "device": {
    "logLevel": "Debug",  // Or "Trace" for maximum detail
    "enableDiagnostics": true
  }
}
```

### Console Commands for Testing

```
// In Crestron CP4N console:
pairZoomRoom <activation-code>
forceRepairZoom
unpairZoomRoom
repairZoomRoom
```

### Inspect SDK Communication

The ZrcSdkController logs all commands and responses at Debug level:
```
[DEBUG] Sending command: SetVolume 75
[DEBUG] Received event: VolumeChanged {"volume": 75}
```

---

## Code Review Checklist

Before submitting a PR, review your own code:

- [ ] Code follows naming conventions
- [ ] No compiler warnings
- [ ] All tests pass (`dotnet test`)
- [ ] Build succeeds (`dotnet build -c Release`)
- [ ] Comments explain "why", not "what"
- [ ] No debug code left in (`Debug.WriteLine()`, etc.)
- [ ] Commit messages are descriptive
- [ ] No unrelated changes in commit
- [ ] Issue/PR numbers referenced in message

---

## Quick Reference: Important Classes

| Class | Purpose | Key Methods |
|-------|---------|------------|
| `ZoomRoom` | Main device | `Connect()`, `StartSharing()`, `SetVolume()` |
| `ZrcSdkController` | SDK communication | `SendCommand()`, `HandleEvent()` |
| `ZoomRoomJoinMap` | SIMPL bridge | `AddAction()`, `AddFeedback()` |
| `ZoomRoomStatus` | State container | Properties for all device state |
| `ZoomRoomConfiguration` | Device config | Activation code, device address |
| `ZoomRoomFactory` | Plugin factory | `CreateDevice()` |

---

## Getting Help

- **How do I...?** → Check [GLOSSARY.md](GLOSSARY.md)
- **Build failed** → Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md#building)
- **Not sure about architecture** → Read [ARCHITECTURE.md](ARCHITECTURE.md)
- **Need to understand SIMPL?** → Check [GLOSSARY.md](GLOSSARY.md#simpl)

---

**Last Updated**: 2026-08-10
**Standards Version**: 1.0
