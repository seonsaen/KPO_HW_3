using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using FileAnalysisService.Entities.Models;
using FileAnalysisService.Repositories;

namespace FileAnalysisService.Services;

public interface IAnalysisService
{
    Task<Report> AnalyzeAsync(string submissionId, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetReportsByAssignmentAsync(string assignmentId, CancellationToken ct = default);
}

public class AnalysisService : IAnalysisService
{
    private readonly IHttpClientFactory _http;
    private readonly IReportRepository _repo;
    private readonly IConfiguration _config;
    private readonly ILogger<AnalysisService> _logger;
    private readonly string _reportsDir;

    public AnalysisService(IHttpClientFactory http, IReportRepository repo, IConfiguration config, ILogger<AnalysisService> logger)
    {
        _http = http;
        _repo = repo;
        _config = config;
        _logger = logger;
        var dataDir = config.GetValue<string>("Storage:DataDir") ?? "/app/data";
        _reportsDir = Path.Combine(dataDir, "reports");
        Directory.CreateDirectory(_reportsDir);
    }

    public async Task<Report> AnalyzeAsync(string submissionId, CancellationToken ct = default)
    {
        var client = _http.CreateClient("filestoring");
        var metaResp = await client.GetAsync($"/files/{WebUtility.UrlEncode(submissionId)}/metadata", ct);
        if (!metaResp.IsSuccessStatusCode) throw new Exception("Submission not found in FileStoring");
        var metaJson = await metaResp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(metaJson);
        var root = doc.RootElement;
        var studentName = root.GetProperty("studentName").GetString()!;
        var assignmentId = root.GetProperty("assignmentId").GetString()!;
        var uploadedAt = root.GetProperty("uploadedAt").GetString()!;
        var fileName = root.GetProperty("fileName").GetString()!;

        var listResp = await client.GetAsync($"/files/works/{WebUtility.UrlEncode(assignmentId)}/submissions", ct);
        listResp.EnsureSuccessStatusCode();
        var listJson = await listResp.Content.ReadAsStringAsync(ct);
        var listEl = JsonDocument.Parse(listJson).RootElement;

        bool isPlag = false;
        string? similarId = null;

        foreach (var item in listEl.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            var sname = item.GetProperty("studentName").GetString();
            if (id == submissionId) break; // ordered by uploadedAt asc, stop at current
            if (!string.Equals(sname, studentName, StringComparison.OrdinalIgnoreCase))
            {
                isPlag = true;
                similarId = id;
                break;
            }
        }

        var dlResp = await client.GetAsync($"/files/{WebUtility.UrlEncode(submissionId)}/download", ct);
        string? wordCloudUrl = null;
        if (dlResp.IsSuccessStatusCode)
        {
            try
            {
                using var stream = await dlResp.Content.ReadAsStreamAsync(ct);
                using var sr = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var text = await sr.ReadToEndAsync();
                var cleaned = new string(text.Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());
                var words = cleaned.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2)
                    .Select(w => w.ToLowerInvariant());

                var freq = words.GroupBy(w => w).Select(g => new { w = g.Key, c = g.Count() })
                                .OrderByDescending(x => x.c).Take(100).ToList();
                if (freq.Any())
                {
                    var textForCloud = string.Join(" ", freq.Select(f => string.Join(' ', Enumerable.Repeat(f.w, Math.Min(f.c, 10)))));
                    var qs = HttpUtility.UrlEncode(textForCloud);
                    wordCloudUrl = $"https://quickchart.io/wordcloud?text={qs}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Unable to produce word cloud");
            }
        }

        var reportId = Guid.NewGuid().ToString();
        var report = new Report(reportId, submissionId, studentName, assignmentId, isPlag, similarId, DateTime.UtcNow, Path.Combine(_reportsDir, $"{reportId}.json"), wordCloudUrl);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(report.ReportPath!, json, ct);

        await _repo.AddAsync(report, ct);

        return report;
    }

    public Task<IReadOnlyList<Report>> GetReportsByAssignmentAsync(string assignmentId, CancellationToken ct = default)
        => _repo.GetByAssignmentAsync(assignmentId, ct);
}

