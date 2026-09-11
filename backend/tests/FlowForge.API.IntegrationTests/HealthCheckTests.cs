using System.Net;
using FluentAssertions;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class HealthCheckTests
{
    private readonly CustomWebApplicationFactory _factory;
    public HealthCheckTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_Health_ReturnsHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("healthy");
    }
}
