using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Features;
using FlowForge.Application.Identity.Services;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.AI.Assistant;

// The API is the gatekeeper: each handler decides who may do what BEFORE the AI service is called.
// The AI service trusts the tenant id it is given, so this is where that trust is earned.

public record AskAssistantQuery(string Question) : IRequest<Result<AssistantAnswerDto>>, IRequiresFeature
{
    public string FeatureKey => FeatureKeys.AiAssistant;
}

public class AskAssistantQueryValidator : AbstractValidator<AskAssistantQuery>
{
    public AskAssistantQueryValidator() => RuleFor(x => x.Question).NotEmpty().MaximumLength(2000);
}

public class AskAssistantQueryHandler : IRequestHandler<AskAssistantQuery, Result<AssistantAnswerDto>>
{
    private readonly ICurrentUser _user;
    private readonly IAiServiceClient _ai;

    public AskAssistantQueryHandler(ICurrentUser user, IAiServiceClient ai) { _user = user; _ai = ai; }

    public Task<Result<AssistantAnswerDto>> Handle(AskAssistantQuery request, CancellationToken ct)
    {
        if (_user.TenantId == null || _user.UserId == null)
            return Task.FromResult(Result.Failure<AssistantAnswerDto>(Error.Unauthorized("Auth.NoTenant", "No active workspace")));
        return _ai.AskAsync(request.Question.Trim(), ct);
    }
}

public record StreamAssistantQuery(string Question) : IRequest<Result<Stream>>, IRequiresFeature
{
    public string FeatureKey => FeatureKeys.AiAssistant;
}

public class StreamAssistantQueryValidator : AbstractValidator<StreamAssistantQuery>
{
    public StreamAssistantQueryValidator() => RuleFor(x => x.Question).NotEmpty().MaximumLength(2000);
}

public class StreamAssistantQueryHandler : IRequestHandler<StreamAssistantQuery, Result<Stream>>
{
    private readonly ICurrentUser _user;
    private readonly IAiServiceClient _ai;

    public StreamAssistantQueryHandler(ICurrentUser user, IAiServiceClient ai) { _user = user; _ai = ai; }

    public Task<Result<Stream>> Handle(StreamAssistantQuery request, CancellationToken ct)
    {
        if (_user.TenantId == null || _user.UserId == null)
            return Task.FromResult(Result.Failure<Stream>(Error.Unauthorized("Auth.NoTenant", "No active workspace")));
        return _ai.StreamAskAsync(request.Question.Trim(), ct);
    }
}

public record ListAssistantDocumentsQuery : IRequest<Result<IReadOnlyList<AssistantDocumentDto>>>, IRequiresFeature
{
    public string FeatureKey => FeatureKeys.AiAssistant;
}

public class ListAssistantDocumentsQueryHandler : IRequestHandler<ListAssistantDocumentsQuery, Result<IReadOnlyList<AssistantDocumentDto>>>
{
    private readonly ICurrentUser _user;
    private readonly IAiServiceClient _ai;

    public ListAssistantDocumentsQueryHandler(ICurrentUser user, IAiServiceClient ai) { _user = user; _ai = ai; }

    public Task<Result<IReadOnlyList<AssistantDocumentDto>>> Handle(ListAssistantDocumentsQuery request, CancellationToken ct)
    {
        if (_user.TenantId == null || _user.UserId == null)
            return Task.FromResult(Result.Failure<IReadOnlyList<AssistantDocumentDto>>(Error.Unauthorized("Auth.NoTenant", "No active workspace")));
        return _ai.ListDocumentsAsync(ct);
    }
}

/// <summary>Uploads a document for the assistant to read. Owners and admins only: documents feed answers for everyone in the workspace.</summary>
public record UploadAssistantDocumentCommand(string FileName, string ContentType, long Size, Stream Content)
    : IRequest<Result<AssistantDocumentDto>>, IRequiresFeature
{
    public string FeatureKey => FeatureKeys.AiAssistant;
}

public static class AssistantDocumentRules
{
    public const long MaxBytes = 25 * 1024 * 1024;
    public static readonly string[] AllowedExtensions = [".pdf", ".txt", ".md", ".markdown"];
}

public class UploadAssistantDocumentCommandValidator : AbstractValidator<UploadAssistantDocumentCommand>
{
    public UploadAssistantDocumentCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Size).GreaterThan(0).LessThanOrEqualTo(AssistantDocumentRules.MaxBytes)
            .WithMessage("Documents can be at most 25 MB.");
        RuleFor(x => x.FileName)
            .Must(n => AssistantDocumentRules.AllowedExtensions.Contains(Path.GetExtension(n).ToLowerInvariant()))
            .WithMessage("Only PDF, TXT and Markdown files are supported.");
    }
}

public class UploadAssistantDocumentCommandHandler : IRequestHandler<UploadAssistantDocumentCommand, Result<AssistantDocumentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IAiServiceClient _ai;

    public UploadAssistantDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user, IAiServiceClient ai)
    { _uow = uow; _user = user; _ai = ai; }

    public async Task<Result<AssistantDocumentDto>> Handle(UploadAssistantDocumentCommand request, CancellationToken ct)
    {
        var access = await WorkspaceAccess.RequireAdminAsync(_uow, _user, ct);
        if (access.IsFailure) return Result.Failure<AssistantDocumentDto>(access.Error);
        return await _ai.UploadDocumentAsync(Path.GetFileName(request.FileName), request.ContentType, request.Content, ct);
    }
}

public record DeleteAssistantDocumentCommand(string DocumentId) : IRequest<Result>, IRequiresFeature
{
    public string FeatureKey => FeatureKeys.AiAssistant;
}

public class DeleteAssistantDocumentCommandHandler : IRequestHandler<DeleteAssistantDocumentCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IAiServiceClient _ai;

    public DeleteAssistantDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user, IAiServiceClient ai)
    { _uow = uow; _user = user; _ai = ai; }

    public async Task<Result> Handle(DeleteAssistantDocumentCommand request, CancellationToken ct)
    {
        var access = await WorkspaceAccess.RequireAdminAsync(_uow, _user, ct);
        if (access.IsFailure) return Result.Failure(access.Error);
        return await _ai.DeleteDocumentAsync(request.DocumentId, ct);
    }
}
