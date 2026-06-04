using FlowForge.Application.Common.Abstractions;

namespace FlowForge.Infrastructure.Services.Auth;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
