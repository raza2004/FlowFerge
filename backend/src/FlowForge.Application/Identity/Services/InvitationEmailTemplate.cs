using System.Net;

namespace FlowForge.Application.Identity.Services;

/// <summary>
/// The invitation email. Email clients ignore most modern CSS, so this is table based with
/// inline styles; the layout holds up in Gmail, Outlook and Apple Mail.
/// </summary>
public static class InvitationEmailTemplate
{
    public static string Render(string inviterName, string workspaceName, string role, string url, DateTime expiresAt)
    {
        var inviter = WebUtility.HtmlEncode(inviterName);
        var workspace = WebUtility.HtmlEncode(workspaceName);
        var roleText = WebUtility.HtmlEncode(role);
        var link = WebUtility.HtmlEncode(url);
        var initial = WebUtility.HtmlEncode(workspaceName.Length > 0 ? workspaceName[..1].ToUpperInvariant() : "F");
        var expires = expiresAt.ToString("MMMM d, yyyy");

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>You're invited to {{workspace}}</title>
</head>
<body style="margin:0;padding:0;background-color:#f3f2f8;font-family:'Segoe UI',Helvetica,Arial,sans-serif;">
<div style="display:none;max-height:0;overflow:hidden;opacity:0;">{{inviter}} invited you to join {{workspace}} on FlowForge.</div>
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f3f2f8;padding:32px 12px;">
  <tr><td align="center">
    <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;width:100%;">
      <tr><td align="center" style="padding-bottom:20px;">
        <table role="presentation" cellpadding="0" cellspacing="0"><tr>
          <td style="background:#6449e0;border-radius:10px;width:34px;height:34px;text-align:center;color:#ffffff;font-weight:700;font-size:18px;line-height:34px;">F</td>
          <td style="padding-left:10px;font-size:20px;font-weight:700;color:#1b1738;letter-spacing:-0.3px;">FlowForge</td>
        </tr></table>
      </td></tr>
      <tr><td style="background:#ffffff;border-radius:16px;border:1px solid #e6e3f3;overflow:hidden;">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
          <tr><td style="height:6px;background:linear-gradient(90deg,#6449e0,#8b73f0,#b7a7f7);font-size:0;line-height:0;">&nbsp;</td></tr>
          <tr><td align="center" style="padding:36px 40px 8px;">
            <table role="presentation" cellpadding="0" cellspacing="0"><tr>
              <td style="background:#efeafd;border-radius:16px;width:64px;height:64px;text-align:center;color:#6449e0;font-weight:700;font-size:28px;line-height:64px;">{{initial}}</td>
            </tr></table>
          </td></tr>
          <tr><td align="center" style="padding:16px 40px 0;">
            <h1 style="margin:0;font-size:24px;line-height:1.3;color:#1b1738;font-weight:700;">Join {{workspace}}</h1>
          </td></tr>
          <tr><td align="center" style="padding:12px 40px 0;font-size:15px;line-height:1.6;color:#5d5a78;">
            <strong style="color:#1b1738;">{{inviter}}</strong> invited you to collaborate in the
            <strong style="color:#1b1738;">{{workspace}}</strong> workspace on FlowForge.
          </td></tr>
          <tr><td align="center" style="padding:18px 40px 0;">
            <span style="display:inline-block;background:#efeafd;color:#4b34b8;border-radius:999px;padding:6px 14px;font-size:13px;font-weight:600;">Your role: {{roleText}}</span>
          </td></tr>
          <tr><td align="center" style="padding:28px 40px 8px;">
            <a href="{{link}}" style="display:inline-block;background:#6449e0;color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:14px 32px;border-radius:10px;">Accept invitation</a>
          </td></tr>
          <tr><td align="center" style="padding:16px 40px 36px;font-size:12px;line-height:1.6;color:#8a87a3;">
            This invitation expires on {{expires}}.<br>
            Button not working? Paste this link into your browser:<br>
            <a href="{{link}}" style="color:#6449e0;word-break:break-all;">{{link}}</a>
          </td></tr>
        </table>
      </td></tr>
      <tr><td align="center" style="padding:20px 12px 0;font-size:12px;line-height:1.6;color:#8a87a3;">
        Not expecting this? You can safely ignore this email and nothing will happen.<br>
        Sent by FlowForge
      </td></tr>
    </table>
  </td></tr>
</table>
</body>
</html>
""";
    }
}
