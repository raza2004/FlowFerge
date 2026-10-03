namespace FlowForge.Application.Common.Abstractions;

/// <summary>Builds links into the frontend for emails and API responses (configured base URL lives in Infrastructure).</summary>
public interface IAppLinks
{
    string InvitationUrl(string token);
}
