using ClaudeCodeGui.Models;

namespace ClaudeCodeGui.Data;

public static class TemplateSeeder
{
    public static async Task SeedDefaultsAsync(JsonFileStore<PromptTemplate> store)
    {
        var existing = await store.GetAllAsync();
        if (existing.Count > 0) return;

        var defaults = new (string Stage, string Name, string Body)[]
        {
            ("requirements", "要件定義", "以下のIssueについて要件定義を行ってください。\n\nタイトル: {{issue.title}}\n説明: {{issue.description}}\n\n対象プロジェクト: {{issue.targetProjectPath}}"),
            ("design", "設計", "以下のIssueについて設計（コンポーネント設計・関数設計）を行ってください。\n\nタイトル: {{issue.title}}\n説明: {{issue.description}}\n\n対象プロジェクト: {{issue.targetProjectPath}}"),
            ("implementation", "実装", "以下のIssueについて実装を行ってください。\n\nタイトル: {{issue.title}}\n説明: {{issue.description}}\n\n対象プロジェクト: {{issue.targetProjectPath}}"),
            ("testing", "テスト", "以下のIssueについてテストを実施してください。\n\nタイトル: {{issue.title}}\n説明: {{issue.description}}\n\n対象プロジェクト: {{issue.targetProjectPath}}"),
            ("deployment", "デプロイ", "以下のIssueについてデプロイ作業を行ってください。\n\nタイトル: {{issue.title}}\n説明: {{issue.description}}\n\n対象プロジェクト: {{issue.targetProjectPath}}"),
        };

        foreach (var (stage, name, body) in defaults)
        {
            await store.SaveAsync(new PromptTemplate { Stage = stage, Name = name, Body = body });
        }
    }
}
