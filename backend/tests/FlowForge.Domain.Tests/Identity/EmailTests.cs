using FlowForge.Domain.Identity.ValueObjects;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Identity;

public class EmailTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last+tag@sub.example.co")]
    public void Create_WithValidEmail_Succeeds(string value)
    {
        var result = Email.Create(value);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(value.ToLowerInvariant());
    }

    [Fact]
    public void Create_LowercasesTheValue()
    {
        var result = Email.Create("Mixed.Case@Example.COM");

        result.Value.Value.Should().Be("mixed.case@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    public void Create_WithInvalidEmail_Fails(string value)
    {
        var result = Email.Create(value);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_TooLong_Fails()
    {
        var localPart = new string('a', 250);
        var result = Email.Create($"{localPart}@example.com");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.TooLong");
    }

    [Fact]
    public void TwoEmails_WithSameValue_AreEqual()
    {
        var a = Email.Create("same@example.com").Value;
        var b = Email.Create("SAME@example.com").Value;

        a.Should().Be(b);
    }
}
