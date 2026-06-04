using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Projects.Events;

public sealed record CommentAddedEvent(Guid CommentId, Guid TaskId, Guid AuthorId, string Content, List<Guid> MentionedUserIds, Guid TenantId) : DomainEvent;
