using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

public record CreateLabelCommand(Guid ProjectId, string Name, string Color) : IRequest<Result<LabelDto>>;

public class CreateLabelCommandValidator : AbstractValidator<CreateLabelCommand>
{
    public CreateLabelCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).NotEmpty().Matches("^#[0-9a-fA-F]{6}$").WithMessage("Color must be a hex value like #6366f1");
    }
}

public class CreateLabelCommandHandler : IRequestHandler<CreateLabelCommand, Result<LabelDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateLabelCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<LabelDto>> Handle(CreateLabelCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<LabelDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project == null || project.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<LabelDto>(Error.NotFound("Project.NotFound", "Project not found"));

        var existing = await _uow.Labels.GetByProjectAsync(request.ProjectId, ct);
        if (existing.Any(l => string.Equals(l.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return Result.Failure<LabelDto>(Error.Conflict("Label.Exists", "A label with that name already exists"));

        var labelResult = Label.Create(request.ProjectId, request.Name, request.Color);
        if (labelResult.IsFailure) return Result.Failure<LabelDto>(labelResult.Error);

        var label = labelResult.Value;
        await _uow.Labels.AddAsync(label, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new LabelDto(label.Id, label.Name, label.Color));
    }
}
