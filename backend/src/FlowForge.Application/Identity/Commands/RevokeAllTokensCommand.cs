using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record RevokeAllTokensCommand(Guid UserId) : IRequest<Result>;

public class RevokeAllTokensCommandHandler : IRequestHandler<RevokeAllTokensCommand, Result>
{
    private readonly IUnitOfWork _uow;

    public RevokeAllTokensCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(RevokeAllTokensCommand request, CancellationToken ct)
    {
        var tokens = await _uow.RefreshTokens.GetActiveByUserAsync(request.UserId, ct);
        foreach (var token in tokens)
        {
            token.Revoke("Revoked by user");
            _uow.RefreshTokens.Update(token);
        }
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
