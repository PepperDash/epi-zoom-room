using FluentAssertions;

namespace PepperDash.Essentials.Plugins.Zoom.Room.Tests;

/// <summary>
/// Validates the IZoomRoomController abstraction contract using MetadataLoadContext
/// (reflection-only, no Crestron runtime required).
///
/// These tests "keep the abstraction honest":
///   - The interface must exist with the correct name.
///   - Every command method and SDK event that ZoomRoom depends on must be present.
///   - The interface must extend IDisposable so controllers are always cleaned up.
///
/// If a future refactor accidentally removes a method or event that ZoomRoom calls,
/// these tests will fail at CI time rather than silently at runtime on hardware.
/// </summary>
public class ControllerAbstractionTests
{
    private static System.Reflection.TypeInfo ControllerInterfaceType =>
        AssemblyFixture.PluginAssembly.DefinedTypes
            .Single(t => t.Name == "IZoomRoomController");

    // ── Interface shape ───────────────────────────────────────────────────────

    [Fact]
    public void IZoomRoomController_Exists_In_Assembly()
    {
        AssemblyFixture.PluginAssembly.DefinedTypes
            .Select(t => t.Name)
            .Should().Contain("IZoomRoomController",
                "the controller abstraction must be present in the plugin assembly");
    }

    [Fact]
    public void IZoomRoomController_Is_Interface()
    {
        ControllerInterfaceType.IsInterface
            .Should().BeTrue("IZoomRoomController must be an interface, not a class");
    }

    [Fact]
    public void IZoomRoomController_Extends_IDisposable()
    {
        ControllerInterfaceType
            .ImplementedInterfaces
            .Select(i => i.Name)
            .Should().Contain("IDisposable",
                "controllers manage native SDK resources and must always be disposable");
    }

    // ── Command methods ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("Initialize")]
    [InlineData("GetConnectionState")]
    [InlineData("PairWithActivationCode")]
    [InlineData("CanRetryPair")]
    [InlineData("RetryPair")]
    [InlineData("Unpair")]
    [InlineData("RepairWithConfiguredCode")]
    public void IZoomRoomController_Has_Lifecycle_Method(string methodName)
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(m => m.Name == methodName,
                $"IZoomRoomController must expose '{methodName}' for connection lifecycle management");
    }

    [Theory]
    [InlineData("StartMeeting")]
    [InlineData("StartInstantMeeting")]
    [InlineData("JoinMeeting")]
    [InlineData("JoinMeetingWithPassword")]
    [InlineData("JoinMeetingWithUrl")]
    [InlineData("EndMeeting")]
    [InlineData("LeaveMeeting")]
    [InlineData("SendMeetingPassword")]
    [InlineData("CancelEnteringMeetingPassword")]
    [InlineData("CancelWaitingForHost")]
    [InlineData("LockMeeting")]
    public void IZoomRoomController_Has_Meeting_Method(string methodName)
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(m => m.Name == methodName,
                $"IZoomRoomController must expose '{methodName}' for meeting control");
    }

    [Theory]
    [InlineData("SetAudioMute")]
    [InlineData("MuteUserAudio")]
    [InlineData("MuteAllAudio")]
    [InlineData("SetMuteOnEntry")]
    [InlineData("AllowAttendeesUnmute")]
    public void IZoomRoomController_Has_Audio_Method(string methodName)
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(m => m.Name == methodName,
                $"IZoomRoomController must expose '{methodName}' for audio control");
    }

    [Theory]
    [InlineData("SetVideoState")]
    [InlineData("MuteUserVideo")]
    [InlineData("PinUserOnScreen")]
    [InlineData("UnpinUserFromScreen")]
    [InlineData("ControlUserCamera")]
    [InlineData("ControlVideoPosition")]
    public void IZoomRoomController_Has_Video_Method(string methodName)
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(m => m.Name == methodName,
                $"IZoomRoomController must expose '{methodName}' for video control");
    }

    [Theory]
    [InlineData("SetScreenLayout")]
    [InlineData("SetVideoOrder")]
    [InlineData("UpdateVideoLayoutStyle")]
    [InlineData("TurnVideoPage")]
    [InlineData("ChangeThumbnailsPosition")]
    public void IZoomRoomController_Has_Layout_Method(string methodName)
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(m => m.Name == methodName,
                $"IZoomRoomController must expose '{methodName}' for layout control");
    }

    [Theory]
    [InlineData("StopShare")]
    [InlineData("LaunchSharingMeeting")]
    [InlineData("SwitchFromLocalPresentationToNormalMeeting")]
    [InlineData("ShowSharingInstruction")]
    public void IZoomRoomController_Has_Share_Method(string methodName)
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(m => m.Name == methodName,
                $"IZoomRoomController must expose '{methodName}' for share control");
    }

    [Theory]
    [InlineData("StartRecording")]
    [InlineData("StopRecording")]
    [InlineData("PauseRecording")]
    [InlineData("ResumeRecording")]
    [InlineData("ResponseToRecordingRequest")]
    public void IZoomRoomController_Has_Recording_Method(string methodName)
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(m => m.Name == methodName,
                $"IZoomRoomController must expose '{methodName}' for recording control");
    }

    [Theory]
    [InlineData("AdmitUserFromWaitingRoom")]
    [InlineData("AdmitAllFromWaitingRoom")]
    [InlineData("PutUserInWaitingRoom")]
    public void IZoomRoomController_Has_WaitingRoom_Method(string methodName)
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(m => m.Name == methodName,
                $"IZoomRoomController must expose '{methodName}' for waiting room control");
    }

    // ── Method return types ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Initialize")]
    [InlineData("PairWithActivationCode")]
    [InlineData("RepairWithConfiguredCode")]
    [InlineData("StartMeeting")]
    [InlineData("StartInstantMeeting")]
    [InlineData("JoinMeeting")]
    [InlineData("JoinMeetingWithPassword")]
    [InlineData("EndMeeting")]
    [InlineData("LeaveMeeting")]
    [InlineData("SendMeetingPassword")]
    [InlineData("LockMeeting")]
    [InlineData("SetAudioMute")]
    [InlineData("MuteAllAudio")]
    [InlineData("SetVideoState")]
    [InlineData("AdmitAllFromWaitingRoom")]
    public void IZoomRoomController_CommandMethod_Returns_Bool(string methodName)
    {
        var method = ControllerInterfaceType.GetMethods()
            .FirstOrDefault(m => m.Name == methodName);

        method.Should().NotBeNull($"'{methodName}' must exist on IZoomRoomController");
        method!.ReturnType.Name
            .Should().Be("Boolean",
                $"'{methodName}' must return bool so callers can detect SDK rejection");
    }

    [Fact]
    public void IZoomRoomController_GetConnectionState_Returns_Int()
    {
        var method = ControllerInterfaceType.GetMethods()
            .FirstOrDefault(m => m.Name == "GetConnectionState");

        method.Should().NotBeNull();
        method!.ReturnType.Name
            .Should().Be("Int32",
                "GetConnectionState returns 0=Established/1=Connected/2=Disconnected as an int");
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Initialized")]
    [InlineData("ConnectionStateChanged")]
    [InlineData("Error")]
    [InlineData("PairRoomResult")]
    [InlineData("MeetingStatusChanged")]
    [InlineData("InstantMeetingStarted")]
    [InlineData("StartPmiResult")]
    [InlineData("ExitMeeting")]
    [InlineData("MeetingNeedsPassword")]
    [InlineData("MeetingLockStatusChanged")]
    [InlineData("AudioMuteStatusChanged")]
    [InlineData("RecordingStatusChanged")]
    [InlineData("RecordingRequestReceived")]
    public void IZoomRoomController_Has_SdkEvent(string eventName)
    {
        ControllerInterfaceType
            .GetEvents()
            .Should().Contain(e => e.Name == eventName,
                $"IZoomRoomController must expose the '{eventName}' event that ZoomRoom subscribes to");
    }

    [Theory]
    [InlineData("ParticipantsInitialized")]
    [InlineData("UserJoined")]
    [InlineData("UserLeft")]
    [InlineData("UserUpdated")]
    [InlineData("ParticipantCountChanged")]
    [InlineData("HostChanged")]
    public void IZoomRoomController_Has_ParticipantEvent(string eventName)
    {
        ControllerInterfaceType
            .GetEvents()
            .Should().Contain(e => e.Name == eventName,
                $"IZoomRoomController must expose the '{eventName}' participant event that ZoomRoom subscribes to");
    }

    [Fact]
    public void IZoomRoomController_Has_SharingStatusChanged_Event()
    {
        ControllerInterfaceType
            .GetEvents()
            .Should().Contain(e => e.Name == "SharingStatusChanged",
                "IZoomRoomController must expose SharingStatusChanged for share-state bridge feedback");
    }

    // ── Password caching API ──────────────────────────────────────────────────

    [Fact]
    public void IZoomRoomController_Has_JoinMeeting_Overload_Without_Password()
    {
        var overloads = ControllerInterfaceType
            .GetMethods()
            .Where(m => m.Name == "JoinMeeting")
            .ToList();

        overloads.Should().Contain(
            m => m.GetParameters().Length == 1
              && m.GetParameters()[0].ParameterType.Name == "String",
            "JoinMeeting(string meetingNumber) must exist so ZoomRoom can join without a password first");
    }

    [Fact]
    public void IZoomRoomController_Has_JoinMeetingWithPassword_Overload()
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(
                m => m.Name == "JoinMeetingWithPassword"
                  && m.GetParameters().Length == 2,
                "JoinMeetingWithPassword(string, string) must exist for the password-caching retry flow");
    }

    [Fact]
    public void IZoomRoomController_Has_SendMeetingPassword()
    {
        ControllerInterfaceType
            .GetMethods()
            .Should().Contain(
                m => m.Name == "SendMeetingPassword"
                  && m.GetParameters().Length == 1,
                "SendMeetingPassword(string) must exist to send the password after MeetingNeedsPassword fires");
    }

    [Fact]
    public void IZoomRoomController_Has_MeetingNeedsPassword_Event()
    {
        ControllerInterfaceType
            .GetEvents()
            .Should().Contain(e => e.Name == "MeetingNeedsPassword",
                "MeetingNeedsPassword event drives the password-caching flow in ZoomRoom");
    }
}
