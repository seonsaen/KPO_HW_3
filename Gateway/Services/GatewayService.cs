using System.Net;
using System.Text;
using System.Text.Json;

namespace Gateway.Services;

public interface IGatewayService
{
    Task<JsonElement> SubmitAndAnalyzeAsync(IFormFile file, string studentName, string assignmentId, CancellationToken ct = default);
    Task<JsonElement> GetReportsAsync(string assignmentId, CancellationToken ct = default);
}

public class GatewayService : IGatewayService
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<GatewayService> _logger;
    public GatewayService(IHttpClientFactory http, ILogger<GatewayService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<JsonElement> SubmitAndAnalyzeAsync(IFormFile file, string studentName, string assignmentId, CancellationToken ct = default)
    {
        var client = _http.CreateClient();
        var fsClient = _http.CreateClient("filestoring");
        var anClient = _http.CreateClient("analysis");

        using var content = new MultipartFormDataContent();
        var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        var fileContent = new StreamContent(ms);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        content.Add(fileContent, "file", file.FileName);
        content.Add(new StringContent(studentName), "studentName");
        content.Add(new StringContent(assignmentId), "assignmentId");

        var uploadResp = await fsClient.PostAsync("/files/upload", content, ct);
        uploadResp.EnsureSuccessStatusCode();
        var uploadJson = await uploadResp.Content.ReadAsStringAsync(ct);

        using var uploadDoc = JsonDocument.Parse(uploadJson);
        var submissionId = uploadDoc.RootElement.GetProperty("submissionId").GetString()!;
        var triggerPayload = JsonSerializer.SerializeToElement(new { submissionId });
        var triggerResp = await anClient.PostAsync("/analysis/trigger", new StringContent(triggerPayload.GetRawText(), Encoding.UTF8, "application/json"), ct);
        triggerResp.EnsureSuccessStatusCode();
        var reportJson = await triggerResp.Content.ReadAsStringAsync(ct);

        using var reportDoc = JsonDocument.Parse(reportJson);
        var combined = JsonSerializer.SerializeToElement(new
        {
            submission = JsonDocument.Parse(uploadJson).RootElement,
            report = reportDoc.RootElement
        });
        return combined;
    }

    public async Task<JsonElement> GetReportsAsync(string assignmentId, CancellationToken ct = default)
    {
        var anClient = _http.CreateClient("analysis");
        var resp = await anClient.GetAsync($"/analysis/reports/by-work/{WebUtility.UrlEncode(assignmentId)}", ct);
        resp.EnsureSuccessStatusCode();
        var txt = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(txt);
        return doc.RootElement.Clone();
    }
}

