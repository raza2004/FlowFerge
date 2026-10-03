using FlowForge.Application.Common.Abstractions;
using Microsoft.Extensions.Configuration;

namespace FlowForge.Infrastructure.Services;

public class AppLinks : IAppLinks
{
    private readonly string _frontendUrl;

    public AppLinks(IConfiguration config)
    {
        _frontendUrl = (config["App:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
    }

    public string InvitationUrl(string token) => $"{_frontendUrl}/invite/{Uri.EscapeDataString(token)}";
}
