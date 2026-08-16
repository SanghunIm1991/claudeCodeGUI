using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ClaudeCodeGui.Data;
using ClaudeCodeGui.Models;

namespace ClaudeCodeGui.Services;

/// <summary>
/// claude CLIをheadlessモード(-p)のサブプロセスとして起動し、
/// stream-json形式の標準出力を1行ずつログファイルへ追記しつつ、
/// SSE購読者へリアルタイムに配信する。
/// </summary>
public class ClaudeRunEngine
{
    private const string HeadlessGuidance =
        "あなたは対話不可のheadless実行(claude -p)で1回だけ呼び出されています。人間に質問して回答を待つことはできません。" +
        "要件や仕様が不明確な場合でも、質問だけを返して停止せず、妥当な前提を明示的に書き添えたうえで、" +
        "可能な範囲まで作業を進めて具体的な成果物（要件定義書・設計書・コード等）を出力してください。";

    private readonly string _claudeCliPath;
    private readonly string _logDir;
    private readonly JsonFileStore<Run> _runStore;
    private readonly ConcurrentDictionary<string, RunContext> _active = new();

    public ClaudeRunEngine(IConfiguration config, string dataRoot, JsonFileStore<Run> runStore)
    {
        _claudeCliPath = config["ClaudeCli:Path"] ?? "claude";
        _logDir = Path.Combine(dataRoot, "run-logs");
        Directory.CreateDirectory(_logDir);
        _runStore = runStore;
    }

    private string LogPathFor(string runId) => Path.Combine(_logDir, $"{runId}.log");

    public async Task<Run> StartAsync(Issue issue, PromptTemplate template, string permissionMode)
    {
        var run = new Run
        {
            IssueId = issue.Id,
            TemplateId = template.Id,
            Stage = template.Stage,
            PermissionMode = permissionMode,
        };

        if (!Directory.Exists(issue.TargetProjectPath))
        {
            run.Status = "failed";
            run.IsError = true;
            run.ResultSummary = $"対象ディレクトリが存在しません: {issue.TargetProjectPath}";
            run.FinishedAt = DateTimeOffset.UtcNow;
            await _runStore.SaveAsync(run);
            return run;
        }

        var prompt = BuildPrompt(template.Body, issue);
        await _runStore.SaveAsync(run);

        var ctx = new RunContext(LogPathFor(run.Id));
        _active[run.Id] = ctx;

        _ = Task.Run(() => ExecuteAsync(run, issue, prompt, permissionMode, ctx));

        return run;
    }

    public static string BuildPrompt(string templateBody, Issue issue) => templateBody
        .Replace("{{issue.title}}", issue.Title)
        .Replace("{{issue.description}}", issue.Description)
        .Replace("{{issue.targetProjectPath}}", issue.TargetProjectPath)
        .Replace("{{issue.currentStage}}", issue.CurrentStage);

    private async Task ExecuteAsync(Run run, Issue issue, string prompt, string permissionMode, RunContext ctx)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _claudeCliPath,
            WorkingDirectory = issue.TargetProjectPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("stream-json");
        psi.ArgumentList.Add("--verbose");
        psi.ArgumentList.Add("--permission-mode");
        psi.ArgumentList.Add(permissionMode);
        psi.ArgumentList.Add("--append-system-prompt");
        psi.ArgumentList.Add(HeadlessGuidance);

        string? lastResultLine = null;

        try
        {
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Start();
            ctx.Process = process;

            await process.StandardInput.WriteAsync(prompt);
            process.StandardInput.Close();

            var stdoutTask = PumpAsync(process.StandardOutput, line =>
            {
                ctx.Append(line);
                if (line.Contains("\"type\":\"result\"")) lastResultLine = line;
            });
            var stderrTask = PumpAsync(process.StandardError, line => ctx.Append($"[stderr] {line}"));

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();

            run.ExitCode = process.ExitCode;
            ApplyResult(run, lastResultLine, process.ExitCode);
        }
        catch (Exception ex)
        {
            run.Status = "failed";
            run.IsError = true;
            run.ResultSummary = $"実行エラー: {ex.Message}";
            ctx.Append($"[error] {ex.Message}");
        }
        finally
        {
            run.FinishedAt = DateTimeOffset.UtcNow;
            await _runStore.SaveAsync(run);
            ctx.Complete();
            _active.TryRemove(run.Id, out _);
        }
    }

    private static void ApplyResult(Run run, string? lastResultLine, int exitCode)
    {
        if (lastResultLine is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(lastResultLine);
                var root = doc.RootElement;
                if (root.TryGetProperty("is_error", out var isErrorProp))
                    run.IsError = isErrorProp.GetBoolean();
                if (root.TryGetProperty("result", out var resultProp))
                    run.ResultSummary = resultProp.GetString();
            }
            catch (JsonException)
            {
                // stream-jsonの最終行が想定外の形式でも実行結果自体は継続して扱う
            }
        }

        run.Status = exitCode == 0 && !run.IsError ? "succeeded" : "failed";
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            onLine(line);
        }
    }

    public async Task<bool> CancelAsync(string runId)
    {
        if (!_active.TryGetValue(runId, out var ctx) || ctx.Process is null) return false;
        try
        {
            ctx.Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            return false; // 既に終了している
        }

        var run = await _runStore.GetAsync(runId);
        if (run is not null && run.Status == "running")
        {
            run.Status = "canceled";
            run.FinishedAt = DateTimeOffset.UtcNow;
            await _runStore.SaveAsync(run);
        }
        return true;
    }

    public bool IsActive(string runId) => _active.ContainsKey(runId);

    public async IAsyncEnumerable<string> StreamLogAsync(string runId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var logPath = LogPathFor(runId);
        var isActive = _active.TryGetValue(runId, out var ctx);

        var sentUpTo = 0;
        if (File.Exists(logPath))
        {
            var existingLines = await File.ReadAllLinesAsync(logPath, ct);
            foreach (var line in existingLines)
            {
                yield return line;
                sentUpTo++;
            }
        }

        if (!isActive || ctx is null) yield break;

        await foreach (var line in ctx.TailAsync(sentUpTo, ct))
        {
            yield return line;
        }
    }

    private class RunContext
    {
        private readonly string _logPath;
        private readonly object _lock = new();
        private readonly List<string> _lines = new();
        private TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _completed;

        public Process? Process { get; set; }

        public RunContext(string logPath)
        {
            _logPath = logPath;
        }

        public void Append(string line)
        {
            lock (_lock)
            {
                _lines.Add(line);
                File.AppendAllText(_logPath, line + Environment.NewLine);
                var old = _signal;
                _signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                old.TrySetResult();
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                _completed = true;
                _signal.TrySetResult();
            }
        }

        public async IAsyncEnumerable<string> TailAsync(int fromIndex, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            var idx = fromIndex;
            while (true)
            {
                List<string>? batch = null;
                var done = false;
                Task waitTask = Task.CompletedTask;

                lock (_lock)
                {
                    if (idx < _lines.Count)
                    {
                        batch = _lines.GetRange(idx, _lines.Count - idx);
                        idx = _lines.Count;
                    }
                    else if (_completed)
                    {
                        done = true;
                    }
                    else
                    {
                        waitTask = _signal.Task;
                    }
                }

                if (batch is not null)
                {
                    foreach (var line in batch) yield return line;
                    continue;
                }

                if (done) yield break;

                await waitTask.WaitAsync(ct);
            }
        }
    }
}
