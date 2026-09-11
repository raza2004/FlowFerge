using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.Commands;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.Repositories;
using FlowForge.Domain.Projects.ValueObjects;
using FluentAssertions;
using Moq;

namespace FlowForge.Application.Tests.Projects;

public class CreateProjectCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public CreateProjectCommandHandlerTests()
    {
        _currentUser.SetupGet(x => x.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);

        _uow.SetupGet(x => x.Projects).Returns(Mock.Of<IProjectRepository>());
        _uow.SetupGet(x => x.Boards).Returns(Mock.Of<IBoardRepository>());
        Mock.Get(_uow.Object.Projects)
            .Setup(x => x.KeyExistsAsync(It.IsAny<Guid>(), It.IsAny<ProjectKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateProjectCommandHandler CreateHandler() => new(_uow.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_CreatesADefaultBoardWithThreeLists()
    {
        Board? capturedBoard = null;
        Mock.Get(_uow.Object.Boards)
            .Setup(x => x.AddAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
            .Callback<Board, CancellationToken>((b, _) => capturedBoard = b)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new CreateProjectCommand("Payments", "PAY", null, null, ProjectVisibility.TenantWide), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedBoard.Should().NotBeNull();
        capturedBoard!.Lists.Should().HaveCount(3);
        capturedBoard.Lists.Select(l => l.Name).Should().Contain(new[] { "To Do", "In Progress", "Done" });
    }

    [Fact]
    public async Task Handle_MarksTheDoneListAsTheDoneColumn()
    {
        // Regression test: the "Done" list used to be created with the default
        // IsDoneColumn=false, so a task dragged into it never actually completed
        // (see MoveTaskCommandHandlerTests for the other half of this bug).
        Board? capturedBoard = null;
        Mock.Get(_uow.Object.Boards)
            .Setup(x => x.AddAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
            .Callback<Board, CancellationToken>((b, _) => capturedBoard = b)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        await handler.Handle(
            new CreateProjectCommand("Payments", "PAY", null, null, ProjectVisibility.TenantWide), CancellationToken.None);

        var doneList = capturedBoard!.Lists.Single(l => l.Name == "Done");
        doneList.IsDoneColumn.Should().BeTrue();

        capturedBoard.Lists.Where(l => l.Name != "Done").Should()
            .OnlyContain(l => !l.IsDoneColumn);
    }

    [Fact]
    public async Task Handle_WithoutAnActiveTenant_Fails()
    {
        _currentUser.SetupGet(x => x.TenantId).Returns((Guid?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new CreateProjectCommand("Payments", "PAY", null, null, ProjectVisibility.TenantWide), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.NoTenant");
    }

    [Fact]
    public async Task Handle_WithADuplicateKey_Fails()
    {
        Mock.Get(_uow.Object.Projects)
            .Setup(x => x.KeyExistsAsync(_tenantId, It.IsAny<ProjectKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new CreateProjectCommand("Payments", "PAY", null, null, ProjectVisibility.TenantWide), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Project.KeyExists");
    }

    [Fact]
    public async Task Handle_MakesTheCreatorAnAdminMemberOfTheProject()
    {
        Project? capturedProject = null;
        Mock.Get(_uow.Object.Projects)
            .Setup(x => x.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((p, _) => capturedProject = p)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        await handler.Handle(
            new CreateProjectCommand("Payments", "PAY", null, null, ProjectVisibility.TenantWide), CancellationToken.None);

        capturedProject!.Members.Should().ContainSingle(m => m.UserId == _userId && m.Role == ProjectMemberRole.Admin);
    }
}
