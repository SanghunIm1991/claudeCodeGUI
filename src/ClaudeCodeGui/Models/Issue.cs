namespace ClaudeCodeGui.Models;

public class Issue
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string TargetProjectPath { get; set; } = "";
    public string CurrentStage { get; set; } = "requirements";
    public string Status { get; set; } = "open"; // open | in_progress | done
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
