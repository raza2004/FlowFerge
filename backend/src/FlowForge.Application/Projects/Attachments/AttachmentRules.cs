namespace FlowForge.Application.Projects.Attachments;

public static class AttachmentRules
{
    public const long MaxFileSizeBytes = 25 * 1024 * 1024;

    // Only raster formats are ever shown inline. SVG and HTML can carry script, so they are
    // always served as downloads, never rendered in the browser on FlowForge's own origin.
    private static readonly HashSet<string> PreviewableImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp"
    };

    public static bool IsPreviewableImage(string contentType) => PreviewableImageTypes.Contains(contentType);

    /// <summary>Keeps letters, digits, dot, dash and underscore so the name is safe inside a storage key.</summary>
    public static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var cleaned = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());
        cleaned = cleaned.Trim('.', '_');
        if (cleaned.Length == 0) cleaned = "file";
        return cleaned.Length > 100 ? cleaned[^100..] : cleaned;
    }
}
