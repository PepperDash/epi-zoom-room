using FluentAssertions;

namespace PepperDash.Essentials.Plugins.Zoom.Room.Tests;

public class FactoryDiscoveryTests
{
    [Fact]
    public void Assembly_Loads_Successfully()
    {
        var act = () => AssemblyFixture.PluginAssembly;
        act.Should().NotThrow("the plugin DLL must be loadable by MetadataLoadContext");
    }

    [Fact]
    public void Assembly_Name_Matches_Expected()
    {
        AssemblyFixture.PluginAssembly.GetName().Name
            .Should().Be("PepperDash.Essentials.Plugins.Zoom.Room",
                "the AssemblyName is derived from the repo name by the 4-Series build workflow");
    }

    [Fact]
    public void Factory_Count_Matches_Expected()
    {
        AssemblyFixture.FindFactoryTypes()
            .Should().HaveCount(1, "epi-zoom-room has exactly one device factory (ZoomRoomFactory)");
    }

    [Theory]
    [InlineData("ZoomRoomFactory")]
    public void Factory_Exists_ByName(string factoryClassName)
    {
        AssemblyFixture.FindFactoryTypes()
            .Select(t => t.Name)
            .Should().Contain(factoryClassName);
    }

    [Fact]
    public void All_Factories_Have_Parameterless_Constructor()
    {
        var factories = AssemblyFixture.FindFactoryTypes();
        foreach (var factory in factories)
        {
            factory.GetConstructor(Type.EmptyTypes)
                .Should().NotBeNull(
                    $"{factory.Name} must have a parameterless constructor — Essentials discovers factories by invoking it");
        }
    }
}
