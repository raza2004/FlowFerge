using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Common.Behaviors;
using FlowForge.Domain.Auditing;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace FlowForge.Application.Tests.Common.Behaviors;

// Deliberately named to match the "*Command" / "*Query" convention the behavior itself
// relies on - this is exactly what production request types look like.
public record FakeCommand;
public record FakeQuery;

public class AuditLoggingBehaviorTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public AuditLoggingBehaviorTests()
    {
        _currentUser.SetupGet(x => x.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);
        _uow.SetupGet(x => x.AuditLogs).Returns(Mock.Of<IAuditLogRepository>());
    }

    private AuditLoggingBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>() where TRequest : notnull =>
        new(_uow.Object, _currentUser.Object, Mock.Of<ILogger<AuditLoggingBehavior<TRequest, TResponse>>>());

    [Fact]
    public async Task Handle_ForASuccessfulCommand_WritesAnAuditLogEntry()
    {
        AuditLog? logged = null;
        Mock.Get(_uow.Object.AuditLogs)
            .Setup(x => x.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((l, _) => logged = l)
            .Returns(Task.CompletedTask);

        var behavior = CreateBehavior<FakeCommand, Result>();
        await behavior.Handle(new FakeCommand(), () => Task.FromResult(Result.Success()), CancellationToken.None);

        logged.Should().NotBeNull();
        logged!.Action.Should().Be(nameof(FakeCommand));
        logged.UserId.Should().Be(_userId);
        logged.TenantId.Should().Be(_tenantId);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForAQuery_NeverWritesAnAuditLogEntry()
    {
        var behavior = CreateBehavior<FakeQuery, Result>();

        await behavior.Handle(new FakeQuery(), () => Task.FromResult(Result.Success()), CancellationToken.None);

        Mock.Get(_uow.Object.AuditLogs).Verify(
            x => x.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ForAFailedCommand_NeverWritesAnAuditLogEntry()
    {
        var behavior = CreateBehavior<FakeCommand, Result>();

        await behavior.Handle(new FakeCommand(),
            () => Task.FromResult(Result.Failure(Error.NotFound("X.NotFound", "not found"))), CancellationToken.None);

        Mock.Get(_uow.Object.AuditLogs).Verify(
            x => x.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutAnActiveTenant_NeverWritesAnAuditLogEntry()
    {
        _currentUser.SetupGet(x => x.TenantId).Returns((Guid?)null);
        var behavior = CreateBehavior<FakeCommand, Result>();

        await behavior.Handle(new FakeCommand(), () => Task.FromResult(Result.Success()), CancellationToken.None);

        Mock.Get(_uow.Object.AuditLogs).Verify(
            x => x.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlwaysReturnsTheInnerHandlerResponse_EvenIfAuditLoggingThrows()
    {
        Mock.Get(_uow.Object.AuditLogs)
            .Setup(x => x.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db is down"));

        var behavior = CreateBehavior<FakeCommand, Result>();
        var expected = Result.Success();

        var response = await behavior.Handle(new FakeCommand(), () => Task.FromResult(expected), CancellationToken.None);

        response.Should().Be(expected);
    }
}
