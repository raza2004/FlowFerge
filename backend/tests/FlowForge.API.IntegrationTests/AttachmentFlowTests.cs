using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Projects.Enums;
using FluentAssertions;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class AttachmentFlowTests
{
    private readonly CustomWebApplicationFactory _factory;
    public AttachmentFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(HttpClient Client, BoardDto Board, TaskCardDto Task)> SetupAsync()
    {
        var tag = Tag();
        var client = _factory.CreateClient();
        var auth = (await (await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            "Att", "Tester", $"att-{tag}@integration.test", "Passw0rd123", $"Files {tag}", $"files-{tag}")))
            .Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var project = (await (await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Files", key = "FIL" + tag[..4].ToUpperInvariant(),
            description = (string?)null, color = (string?)null, visibility = (int)ProjectVisibility.TenantWide
        })).Content.ReadFromJsonAsync<ProjectDto>())!;
        var board = (await (await client.GetAsync($"/api/v1/projects/{project.Id}/boards")).Content.ReadFromJsonAsync<List<BoardDto>>())![0];
        var task = (await (await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = project.Id, boardId = board.Id, listId = board.Lists[0].Id,
            title = "Needs files", description = (string?)null, type = 0, priority = 2
        })).Content.ReadFromJsonAsync<TaskCardDto>())!;
        return (client, board, task);
    }

    private static MultipartFormDataContent FileContent(string fileName, string contentType, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private static Task<HttpResponseMessage> Upload(HttpClient client, Guid taskId, string fileName, string contentType, byte[] bytes) =>
        client.PostAsync($"/api/v1/tasks/{taskId}/attachments", FileContent(fileName, contentType, bytes));

    [Fact]
    public async Task UploadedFile_ComesBackByteForByte_AndShowsOnTheTaskAndCard()
    {
        var (client, board, task) = await SetupAsync();
        var bytes = Encoding.UTF8.GetBytes("quarterly numbers, version 3");

        var upload = await Upload(client, task.Id, "report.txt", "text/plain", bytes);
        upload.EnsureSuccessStatusCode();
        var attachment = (await upload.Content.ReadFromJsonAsync<AttachmentDto>())!;
        attachment.FileName.Should().Be("report.txt");
        attachment.FileSizeBytes.Should().Be(bytes.Length);

        var download = await client.GetAsync($"/api/v1/attachments/{attachment.Id}/content");
        download.EnsureSuccessStatusCode();
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
        download.Content.Headers.ContentDisposition!.FileName.Should().Be("report.txt");

        var detail = (await (await client.GetAsync($"/api/v1/tasks/{task.Id}")).Content.ReadFromJsonAsync<TaskDetailDto>())!;
        detail.Attachments.Should().ContainSingle(a => a.Id == attachment.Id);

        var card = (await (await client.GetAsync($"/api/v1/boards/{board.Id}")).Content.ReadFromJsonAsync<BoardDto>())!
            .Lists.SelectMany(l => l.Tasks).Single(t => t.Id == task.Id);
        card.AttachmentCount.Should().Be(1);
    }

    [Fact]
    public async Task OnlyRasterImagesCanBeShownInline_EverythingElseIsAForcedDownload()
    {
        var (client, _, task) = await SetupAsync();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        var svg = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>");

        var image = (await (await Upload(client, task.Id, "pic.png", "image/png", png)).Content.ReadFromJsonAsync<AttachmentDto>())!;
        var script = (await (await Upload(client, task.Id, "evil.svg", "image/svg+xml", svg)).Content.ReadFromJsonAsync<AttachmentDto>())!;

        image.IsPreviewableImage.Should().BeTrue();
        script.IsPreviewableImage.Should().BeFalse();

        var inlineImage = await client.GetAsync($"/api/v1/attachments/{image.Id}/content?inline=true");
        inlineImage.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        inlineImage.Content.Headers.ContentDisposition?.DispositionType.Should().Be("inline");

        var inlineSvg = await client.GetAsync($"/api/v1/attachments/{script.Id}/content?inline=true");
        inlineSvg.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
        inlineSvg.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        inlineSvg.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
    }

    [Fact]
    public async Task EmptyAndOversizedFiles_AreRejected()
    {
        var (client, _, task) = await SetupAsync();

        (await Upload(client, task.Id, "empty.txt", "text/plain", Array.Empty<byte>())).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        var tooBig = new byte[26 * 1024 * 1024];
        (await Upload(client, task.Id, "huge.bin", "application/octet-stream", tooBig)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeletingAnAttachment_RemovesItFromTheTask_AndItsFileIsGone()
    {
        var (client, board, task) = await SetupAsync();
        var attachment = (await (await Upload(client, task.Id, "temp.txt", "text/plain", Encoding.UTF8.GetBytes("bye")))
            .Content.ReadFromJsonAsync<AttachmentDto>())!;

        (await client.DeleteAsync($"/api/v1/attachments/{attachment.Id}?boardId={board.Id}&taskId={task.Id}")).EnsureSuccessStatusCode();

        var detail = (await (await client.GetAsync($"/api/v1/tasks/{task.Id}")).Content.ReadFromJsonAsync<TaskDetailDto>())!;
        detail.Attachments.Should().BeEmpty();
        (await client.GetAsync($"/api/v1/attachments/{attachment.Id}/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnotherWorkspace_CannotDownloadOrDeleteYourFiles()
    {
        var (owner, _, task) = await SetupAsync();
        var (outsider, _, _) = await SetupAsync();
        var attachment = (await (await Upload(owner, task.Id, "private.txt", "text/plain", Encoding.UTF8.GetBytes("secret")))
            .Content.ReadFromJsonAsync<AttachmentDto>())!;

        (await outsider.GetAsync($"/api/v1/attachments/{attachment.Id}/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.DeleteAsync($"/api/v1/attachments/{attachment.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Upload(outsider, task.Id, "intruder.txt", "text/plain", Encoding.UTF8.GetBytes("x"))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }
}
