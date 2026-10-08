using FlowForge.Application.AI.Queries;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Repositories;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.Repositories;
using FlowForge.Domain.Projects.ValueObjects;
using FlowForge.Shared.Results;
using FluentAssertions;
using Moq;

namespace FlowForge.Application.Tests.AI;

public class GetProjectBlockersQueryHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Mock<IAiService> _ai = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Project _project;
    private readonly List<ProjectTask> _tasks = new();

    public GetProjectBlockersQueryHandlerTests()
    {
        _project = Project.Create(_tenantId, "Apollo", ProjectKey.Create("APOL").Value, Guid.NewGuid(), Guid.NewGuid()).Value;

        var projects = new Mock<IProjectRepository>();
        projects.Setup(x => x.GetByIdAsync(_project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_project);
        _uow.SetupGet(x => x.Projects).Returns(projects.Object);

        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(x => x.GetByProjectAsync(_project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => _tasks);
        _uow.SetupGet(x => x.Tasks).Returns(tasks.Object);

        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<User>());
        _uow.SetupGet(x => x.Users).Returns(users.Object);
    }

    private Task<Result<FlowForge.Application.AI.DTOs.ProjectBlockersDto>> Run()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.TenantId).Returns(_tenantId);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTime.UtcNow);

        return new GetProjectBlockersQueryHandler(_uow.Object, currentUser.Object, _ai.Object, clock.Object)
            .Handle(new GetProjectBlockersQuery(_project.Id), CancellationToken.None);
    }

    private void AddOverdueTask()
    {
        var task = ProjectTask.Create(_tenantId, _project.Id, Guid.NewGuid(), Guid.NewGuid(), "APOL-1", "Late thing",
            TaskType.Task, TaskPriority.High, Guid.NewGuid()).Value;
        task.SetDueDate(DateTime.UtcNow.Date.AddDays(-10));
        _tasks.Add(task);
    }

    [Fact]
    public async Task NothingWrong_ReturnsAHealthyResult_WithoutCallingTheAi()
    {
        var result = await Run();

        result.IsSuccess.Should().BeTrue();
        result.Value.Signals.Should().BeEmpty();
        result.Value.AiAvailable.Should().BeTrue();
        result.Value.AiMessage.Should().Contain("No blockers");
        _ai.Verify(x => x.AnalyzeBlockersAsync(It.IsAny<BlockerAnalysisInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenTheAiWorks_ItsSummaryAndRecommendationsAreIncluded_AndItOnlySeesTheDetectedFindings()
    {
        AddOverdueTask();
        BlockerAnalysisInput? seen = null;
        _ai.Setup(x => x.AnalyzeBlockersAsync(It.IsAny<BlockerAnalysisInput>(), It.IsAny<CancellationToken>()))
            .Callback<BlockerAnalysisInput, CancellationToken>((input, _) => seen = input)
            .ReturnsAsync(Result.Success(new BlockerAnalysis("One task is badly late.", new List<string> { "Reassign APOL-1" })));

        var result = await Run();

        result.Value.AiSummary.Should().Be("One task is badly late.");
        result.Value.Recommendations.Should().Equal("Reassign APOL-1");
        seen!.ProjectName.Should().Be("Apollo");
        seen.SignalLines.Should().ContainSingle().Which.Should().Contain("APOL-1").And.Contain("Overdue by 10 days");
    }

    [Theory]
    [InlineData("AI.NotConfigured", "no AI API key")]
    [InlineData("AI.RequestFailed", "unavailable right now")]
    public async Task WhenTheAiFails_TheRuleBasedFindingsAreStillReturned(string errorCode, string messagePart)
    {
        AddOverdueTask();
        _ai.Setup(x => x.AnalyzeBlockersAsync(It.IsAny<BlockerAnalysisInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<BlockerAnalysis>(Error.Failure(errorCode, "boom")));

        var result = await Run();

        result.IsSuccess.Should().BeTrue("a model outage must never hide real findings");
        result.Value.Signals.Should().ContainSingle();
        result.Value.AiAvailable.Should().BeFalse();
        result.Value.AiSummary.Should().BeNull();
        result.Value.AiMessage.Should().Contain(messagePart);
    }
}
