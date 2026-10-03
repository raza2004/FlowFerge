using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

public record DeleteCommentCommand(Guid CommentId) : IRequest<Result>;

public class DeleteCommentCommandHandler : IRequestHandler<DeleteCommentCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteCommentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteCommentCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var comment = await _uow.TaskComments.GetByIdAsync(request.CommentId, ct);
        if (comment == null || comment.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Comment.NotFound", "Comment not found"));

        if (comment.AuthorId != _currentUser.UserId.Value)
            return Result.Failure(Error.Forbidden("Comment.NotAuthor", "Only the author can delete this comment"));

        comment.SoftDelete(_currentUser.UserId.Value);

        var task = await _uow.Tasks.GetByIdAsync(comment.TaskId, ct);
        task?.DecrementCommentCount();

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
