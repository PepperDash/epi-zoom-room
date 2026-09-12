using FluentAssertions;

namespace PepperDash.Essentials.Plugins.Zoom.Room.Tests;

/// <summary>
/// Source-file scanning tests. MetadataLoadContext cannot execute constructors,
/// so MinimumEssentialsFrameworkVersion and TypeNames (set in the factory ctor) are
/// verified by scanning the compiled source files directly.
/// The ProjectReference in the csproj guarantees the plugin is rebuilt before these
/// tests run, so the source always matches what's in the DLL.
/// </summary>
public class FactoryMetadataTests
{
    private static readonly string[] SourceFiles =
        Directory.GetFiles(AssemblyFixture.SourceDirectory, "*.cs", SearchOption.AllDirectories);

    [Theory]
    [InlineData("ZoomRoomFactory")]
    public void Factory_Source_Sets_MinimumEssentialsFrameworkVersion_To_3(string factoryClassName)
    {
        var factorySource = FindSourceFor(factoryClassName);
        factorySource.Should().NotBeNull($"source file for {factoryClassName} must exist in src/");

        factorySource!
            .Should().Contain("MinimumEssentialsFrameworkVersion = \"3.0.0\"",
                $"{factoryClassName} must target Essentials v3 minimum");
    }

    [Theory]
    [InlineData("ZoomRoomFactory")]
    public void Factory_Source_Assigns_TypeNames(string factoryClassName)
    {
        var factorySource = FindSourceFor(factoryClassName);
        factorySource.Should().NotBeNull($"source file for {factoryClassName} must exist in src/");

        factorySource!
            .Should().Contain("TypeNames",
                $"{factoryClassName} must assign TypeNames so Essentials can instantiate the device");
    }

    [Theory]
    [InlineData("ZoomRoomFactory", "zoomroom")]
    public void Factory_Source_Contains_TypeName(string factoryClassName, string typeName)
    {
        var factorySource = FindSourceFor(factoryClassName);
        factorySource.Should().NotBeNull($"source file for {factoryClassName} must exist in src/");

        factorySource!
            .Should().Contain($"\"{typeName}\"",
                $"{factoryClassName} must register type name \"{typeName}\"");
    }

    [Fact]
    public void No_Duplicate_TypeNames_Across_Factories()
    {
        // Extract all quoted strings from TypeNames assignment blocks
        var allTypeNames = new List<string>();
        foreach (var file in SourceFiles)
        {
            var content = File.ReadAllText(file);
            if (!content.Contains("TypeNames")) continue;

            // Collect every "lowercase-identifier" string literal in the file as a candidate
            var matches = System.Text.RegularExpressions.Regex.Matches(
                content, "\"([a-z][a-z0-9]*)\"");
            foreach (System.Text.RegularExpressions.Match m in matches)
                allTypeNames.Add(m.Groups[1].Value);
        }

        allTypeNames.Should().OnlyHaveUniqueItems("TypeNames must be unique across all factories");
    }

    private static string? FindSourceFor(string className)
    {
        foreach (var file in SourceFiles)
        {
            var content = File.ReadAllText(file);
            if (content.Contains($"class {className}"))
                return content;
        }
        return null;
    }
}
