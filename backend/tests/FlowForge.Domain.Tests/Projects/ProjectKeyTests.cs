using FlowForge.Domain.Projects.ValueObjects;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Projects;

public class ProjectKeyTests
{
    [Theory]
    [InlineData("AB")]
    [InlineData("PROJ")]
    [InlineData("PROJECT10")]
    public void Create_WithValidKey_Succeeds(string value)
    {
        var result = ProjectKey.Create(value);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]                  // too short
    [InlineData("THISISWAYTOOLONG")]   // too long
    [InlineData("proj")]               // lowercase
    [InlineData("1PROJ")]              // starts with a digit
    [InlineData("PROJ-1")]             // hyphen not allowed
    public void Create_WithInvalidKey_Fails(string value)
    {
        var result = ProjectKey.Create(value);

        result.IsFailure.Should().BeTrue();
    }
}
