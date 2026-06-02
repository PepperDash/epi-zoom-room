using FluentAssertions;

namespace PepperDash.Essentials.Plugins.Zoom.Room.Tests;

/// <summary>
/// Validates the DeviceConfig.Properties contract for ZoomRoomPropertiesConfig
/// using MetadataLoadContext (reflection-only, no Crestron runtime required).
/// Tests inspect the compiled assembly, not source files, so they catch drift
/// between the JSON contract and what's actually built.
/// </summary>
public class ConfigDeserializationTests
{
    private static System.Reflection.TypeInfo PropertiesConfigType =>
        AssemblyFixture.PluginAssembly.DefinedTypes
            .Single(t => t.Name == "ZoomRoomPropertiesConfig");

    [Fact]
    public void ZoomRoomPropertiesConfig_Exists_In_Assembly()
    {
        AssemblyFixture.PluginAssembly.DefinedTypes
            .Select(t => t.Name)
            .Should().Contain("ZoomRoomPropertiesConfig",
                "the device properties config class must be present in the plugin assembly");
    }

    [Fact]
    public void ZoomRoomPropertiesConfig_Has_Parameterless_Constructor()
    {
        PropertiesConfigType
            .GetConstructor(Type.EmptyTypes)
            .Should().NotBeNull(
                "Newtonsoft.Json requires a parameterless constructor for deserialization");
    }

    [Theory]
    [InlineData("communicationMonitorProperties")]
    [InlineData("disablePhonebookAutoDownload")]
    [InlineData("supportsCameraAutoMode")]
    [InlineData("supportsCameraOff")]
    [InlineData("autoDefaultLayouts")]
    [InlineData("defaultSharingLayout")]
    [InlineData("defaultCallLayout")]
    [InlineData("minutesBeforeMeetingStart")]
    public void ZoomRoomPropertiesConfig_Property_Has_JsonPropertyAttribute(string jsonName)
    {
        var hasAttribute = PropertiesConfigType
            .GetProperties()
            .Any(p => p.CustomAttributes.Any(a =>
                a.AttributeType.Name == "JsonPropertyAttribute"
                && a.ConstructorArguments.Any(arg =>
                    string.Equals(arg.Value?.ToString(), jsonName, StringComparison.Ordinal))));

        hasAttribute.Should().BeTrue(
            $"ZoomRoomPropertiesConfig must have a property decorated with [JsonProperty(\"{jsonName}\")]");
    }
}
