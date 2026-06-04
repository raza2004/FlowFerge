using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.ValueObjects;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

public record CreateProjectCommand(
    string Name,
    string Key,
    string? Description,
    string? Color,
    ProjectVisibility Visibility
) : IRequest<Result<ProjectDto>>;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(10);
    }
}

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Result<ProjectDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateProjectCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ProjectDto>> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<ProjectDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var keyResult = ProjectKey.Create(request.Key.ToUpperInvariant());
        if (keyResult.IsFailure) return Result.Failure<ProjectDto>(keyResult.Error);

        if (await _uow.Projects.KeyExistsAsync(_currentUser.TenantId.Value, keyResult.Value, ct))
            return Result.Failure<ProjectDto>(Error.Conflict("Project.KeyExists", "Project key already used"));

        var projectResult = Project.Create(
            _currentUser.TenantId.Value,
            request.Name,
            keyResult.Value,
            _currentUser.UserId.Value,
            _currentUser.UserId.Value,
            request.Visibility);

        if (projectResult.IsFailure) return Result.Failure<ProjectDto>(projectResult.Error);

        var project = projectResult.Value;
        if (request.Color != null) project.UpdateDetails(request.Name, request.Description, request.Color, null, null);
        project.AddMember(_currentUser.UserId.Value, ProjectMemberRole.Admin);

        await _uow.Projects.AddAsync(project, ct);

        var boardResult = Board.Create(project.Id, _currentUser.TenantId.Value, "Main Board", BoardType.Kanban, _currentUser.UserId.Value, isDefault: true);
        if (boardResult.IsSuccess)
        {
            var board = boardResult.Value;
            var todoList = BoardList.Create(board.Id, _currentUser.TenantId.Value, "To Do", "#94a3b8");
            var inProgressList = BoardList.Create(board.Id, _currentUser.TenantId.Value, "In Progress", "#3b82f6");
            var doneList = BoardList.Create(board.Id, _currentUser.TenantId.Value, "Done", "#10b981");

            if (todoList.IsSuccess) board.AddList(todoList.Value);
            if (inProgressList.IsSuccess) board.AddList(inProgressList.Value);
            if (doneList.IsSuccess) board.AddList(doneList.Value);

            await _uow.Boards.AddAsync(board, ct);
        }

        await _uow.SaveChangesAsync(ct);

        return Result.Success(new ProjectDto(
            project.Id, project.Name, project.Key.Value, project.Description,
            project.Color, project.Status.ToString(), project.Visibility.ToString(),
            project.OwnerId, null, project.StartDate, project.EndDate,
            project.Boards.Count, 0, project.Members.Count, project.CreatedAt
        ));
    }
}
