using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<Result>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IUnitOfWork _uow;

    public LogoutCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        var token = await _uow.RefreshTokens.GetByTokenAsync(request.RefreshToken, ct);
        if (token == null) return Result.Success();

        if (token.IsActive)
        {
            token.Revoke("User logout");
            _uow.RefreshTokens.Update(token);
            await _uow.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
