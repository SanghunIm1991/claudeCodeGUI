namespace ClaudeCodeGui.Models;

public class Run
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string IssueId { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string Stage { get; set; } = "";
    public string PermissionMode { get; set; } = "acceptEdits";
    public string Status { get; set; } = "running"; // running | succeeded | failed | canceled
    public int? ExitCode { get; set; }
    public string? ResultSummary { get; set; }
    public bool IsError { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
}
