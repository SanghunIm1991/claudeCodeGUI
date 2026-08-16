using ClaudeCodeGui.Data;
using ClaudeCodeGui.Models;
using ClaudeCodeGui.Services;

var builder = WebApplication.CreateBuilder(args);

var dataRoot = Path.Combine(builder.Environment.ContentRootPath, "runtime-data");
Directory.CreateDirectory(dataRoot);

builder.Services.AddSingleton(new JsonFileStore<Issue>(dataRoot, "issues", i => i.Id));
builder.Services.AddSingleton(new JsonFileStore<PromptTemplate>(dataRoot, "templates", t => t.Id));
builder.Services.AddSingleton(new JsonFileStore<Run>(dataRoot, "runs", r => r.Id));
builder.Services.AddSingleton(sp => new ClaudeRunEngine(
    builder.Configuration, dataRoot, sp.GetRequiredService<JsonFileStore<Run>>()));
builder.Services.AddSingleton<ArtifactService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await TemplateSeeder.SeedDefaultsAsync(app.Services.GetRequiredService<JsonFileStore<PromptTemplate>>());
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// ---- Issues ----
app.MapGet("/api/issues", async (JsonFileStore<Issue> store) =>
    Results.Ok((await store.GetAllAsync()).OrderByDescending(i => i.UpdatedAt)));

app.MapGet("/api/issues/{id}", async (string id, JsonFileStore<Issue> store) =>
    await store.GetAsync(id) is { } issue ? Results.Ok(issue) : Results.NotFound());

app.MapPost("/api/issues", async (CreateIssueRequest req, JsonFileStore<Issue> store) =>
{
    var issue = new Issue
    {
        Title = req.Title,
        Description = req.Description ?? "",
        TargetProjectPath = req.TargetProjectPath,
    };
    await store.SaveAsync(issue);
    return Results.Created($"/api/issues/{issue.Id}", issue);
});

app.MapPut("/api/issues/{id}", async (string id, UpdateIssueRequest req, JsonFileStore<Issue> store) =>
{
    var issue = await store.GetAsync(id);
    if (issue is null) return Results.NotFound();

    issue.Title = req.Title;
    issue.Description = req.Description ?? "";
    issue.TargetProjectPath = req.TargetProjectPath;
    issue.CurrentStage = req.CurrentStage;
    issue.Status = req.Status;
    issue.UpdatedAt = DateTimeOffset.UtcNow;
    await store.SaveAsync(issue);
    return Results.Ok(issue);
});

app.MapDelete("/api/issues/{id}", async (string id, JsonFileStore<Issue> store) =>
{
    await store.DeleteAsync(id);
    return Results.NoContent();
});

// ---- Prompt templates ----
app.MapGet("/api/templates", async (JsonFileStore<PromptTemplate> store) =>
    Results.Ok((await store.GetAllAsync()).OrderBy(t => t.Stage).ThenBy(t => t.Name)));

app.MapGet("/api/templates/{id}", async (string id, JsonFileStore<PromptTemplate> store) =>
    await store.GetAsync(id) is { } t ? Results.Ok(t) : Results.NotFound());

app.MapPost("/api/templates", async (SaveTemplateRequest req, JsonFileStore<PromptTemplate> store) =>
{
    var template = new PromptTemplate { Name = req.Name, Stage = req.Stage, Body = req.Body };
    await store.SaveAsync(template);
    return Results.Created($"/api/templates/{template.Id}", template);
});

app.MapPut("/api/templates/{id}", async (string id, SaveTemplateRequest req, JsonFileStore<PromptTemplate> store) =>
{
    var template = await store.GetAsync(id);
    if (template is null) return Results.NotFound();

    template.Name = req.Name;
    template.Stage = req.Stage;
    template.Body = req.Body;
    template.UpdatedAt = DateTimeOffset.UtcNow;
    await store.SaveAsync(template);
    return Results.Ok(template);
});

app.MapDelete("/api/templates/{id}", async (string id, JsonFileStore<PromptTemplate> store) =>
{
    await store.DeleteAsync(id);
    return Results.NoContent();
});

// ---- Runs ----
app.MapGet("/api/issues/{issueId}/runs", async (string issueId, JsonFileStore<Run> store) =>
    Results.Ok((await store.GetAllAsync())
        .Where(r => r.IssueId == issueId)
        .OrderByDescending(r => r.StartedAt)));

app.MapPost("/api/issues/{issueId}/runs", async (
    string issueId, StartRunRequest req,
    JsonFileStore<Issue> issueStore, JsonFileStore<PromptTemplate> templateStore, ClaudeRunEngine engine) =>
{
    var issue = await issueStore.GetAsync(issueId);
    if (issue is null) return Results.NotFound(new { error = "Issueが見つかりません。" });

    var template = await templateStore.GetAsync(req.TemplateId);
    if (template is null) return Results.NotFound(new { error = "テンプレートが見つかりません。" });

    var run = await engine.StartAsync(issue, template, req.PermissionMode ?? "acceptEdits");
    return Results.Accepted($"/api/runs/{run.Id}", run);
});

app.MapGet("/api/runs/{id}", async (string id, JsonFileStore<Run> store) =>
    await store.GetAsync(id) is { } run ? Results.Ok(run) : Results.NotFound());

app.MapPost("/api/runs/{id}/cancel", async (string id, ClaudeRunEngine engine) =>
    await engine.CancelAsync(id) ? Results.Ok() : Results.NotFound());

app.MapGet("/api/runs/{id}/stream", async (string id, HttpContext ctx, ClaudeRunEngine engine) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    await foreach (var line in engine.StreamLogAsync(id, ctx.RequestAborted))
    {
        await ctx.Response.WriteAsync($"data: {line}\n\n", ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
    }
});

// ---- Artifacts (対象プロジェクトのファイル閲覧・編集) ----
app.MapGet("/api/issues/{issueId}/artifacts", async (string issueId, string? path, JsonFileStore<Issue> issueStore, ArtifactService artifacts) =>
{
    var issue = await issueStore.GetAsync(issueId);
    if (issue is null) return Results.NotFound();
    try
    {
        return Results.Ok(artifacts.List(issue.TargetProjectPath, path ?? ""));
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/issues/{issueId}/artifacts/content", async (string issueId, string path, JsonFileStore<Issue> issueStore, ArtifactService artifacts) =>
{
    var issue = await issueStore.GetAsync(issueId);
    if (issue is null) return Results.NotFound();
    try
    {
        return Results.Ok(new { path, content = artifacts.ReadFile(issue.TargetProjectPath, path) });
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound();
    }
});

app.MapPut("/api/issues/{issueId}/artifacts/content", async (string issueId, string path, WriteArtifactRequest req, JsonFileStore<Issue> issueStore, ArtifactService artifacts) =>
{
    var issue = await issueStore.GetAsync(issueId);
    if (issue is null) return Results.NotFound();
    try
    {
        artifacts.WriteFile(issue.TargetProjectPath, path, req.Content);
        return Results.Ok();
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

record CreateIssueRequest(string Title, string? Description, string TargetProjectPath);
record UpdateIssueRequest(string Title, string? Description, string TargetProjectPath, string CurrentStage, string Status);
record SaveTemplateRequest(string Name, string Stage, string Body);
record StartRunRequest(string TemplateId, string? PermissionMode);
record WriteArtifactRequest(string Content);
