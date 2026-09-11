using Xunit;

namespace FlowForge.API.IntegrationTests;

/// <summary>
/// Shares one CustomWebApplicationFactory (and its one PostgreSQL container) across
/// every test class in this project, instead of paying container startup cost per class.
/// Tests still need their own unique data (email, slug, etc.) since the database itself
/// is shared for the whole run - there's no per-test transaction rollback here.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
