using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

public record EditCommentCommand(Guid CommentId, string Content) : IRequest<Result>;

public class EditCommentCommandValidator : AbstractValidator<EditCommentCommand>
{
    public EditCommentCommandValidator()
    {
        RuleFor(x => x.CommentId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(10000);
    }
}

public class EditCommentCommandHandler : IRequestHandler<EditCommentCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public EditCommentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(EditCommentCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var comment = await _uow.TaskComments.GetByIdAsync(request.CommentId, ct);
        if (comment == null || comment.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Comment.NotFound", "Comment not found"));

        var editResult = comment.Edit(request.Content.Trim(), _currentUser.UserId.Value);
        if (editResult.IsFailure) return editResult;

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
