# COMP-05 `ClaudeRunEngine` 拡張

**対象ファイル**: `src/ClaudeCodeGui/Services/ClaudeRunEngine.cs`

**責務**: component_design.md 3.3節COMP-05（195〜304行目）を参照（内容は変更しない）。本節はそこで確定済みの排他制御方式（`_activeIssueRuns`・`GetOrAdd`）・モック分岐・完了通知イベント・`IsCanceled`競合修正を、関数単位の入出力仕様・境界値・分岐条件までテストケース設計に転用できる粒度で具体化する。COMP-01〜04と異なり既存クラス内の複数メソッドにまたがる変更のため、対象メソッドごとに2.5.1〜2.5.5節へ分けて記載する。

小節構成は以下のとおり。

| 小節 | 対象メソッド／要素 | 内容 |
|---|---|---|
| 2.5.1 | `StartAsync`（新シグネチャ） | Issue単位の排他制御（`_activeIssueRuns`）、ロック解放方針 |
| 2.5.2 | `ExecuteAsync`のモック分岐 | `isMock`による処理・副作用の違い |
| 2.5.3 | `CancelAsync`と`ExecuteAsync`の`finally` | `IsCanceled`をめぐる競合の解消 |
| 2.5.4 | `RunContext` | `IsCanceled`フィールドの追加 |
| 2.5.5 | `RunCompleted`イベント | Run完了通知イベントの入出力 |

#### 2.5.1 `StartAsync`（新シグネチャ）

```csharp
public async Task<RunStartResult> StartAsync(
    Issue issue, PromptTemplate template, string permissionMode, bool triggeredByLoop = false)
```

新規record: `public record RunStartResult(Run Run, string? ConflictingRunId);`

**事前条件**:

| 引数 | 制約 |
|---|---|
| `issue` | null不可。`Id`・`TargetProjectPath`が設定済みの既存`Issue` |
| `template` | null不可。`Id`・`Stage`・`Body`が設定済みの既存`PromptTemplate` |
| `permissionMode` | null不可の`string`。値の妥当性検証は行わない（既存動作維持。COMP-01「補助関数の要否」節で申し送り済みの未解決欠落であり、本節では新規のバリデーション関数を追加しない） |
| `triggeredByLoop` | 既定`false`。手動実行系（COMP-11 `POST /api/issues/{issueId}/runs`ハンドラ）は指定しない＝常に`false`（REQ-21）。`LoopEngine`（COMP-08）が`true`を指定する経路の詳細はCOMP-02関数設計2.2節「`TriggeredByLoop`の書き込み経路」参照 |

**処理の骨格**（component_design.md 228〜260行目の処理フロー・ロック解放方針をそのまま踏襲）:

1. `run = new Run { IssueId = issue.Id, TemplateId = template.Id, Stage = template.Stage, PermissionMode = permissionMode }` を構築する（`Run.Id`はプロパティ初期化子で採番済み・まだ未保存）。
2. `var prompt = BuildPrompt(template.Body, issue);` でプロンプト文字列を構築する（既存の静的メソッド、変更なし）。
3. `var winningRunId = _activeIssueRuns.GetOrAdd(issue.Id, run.Id);` を呼ぶ（Issue単位ロックのアトミックな獲得試行）。
4. `winningRunId != run.Id` なら「排他拒否系」（2.5.1-A）、`winningRunId == run.Id` なら「正常系」（2.5.1-B）へ分岐する。

##### 2.5.1-A 排他拒否系（ロック獲得失敗）の入出力

| 項目 | 内容 |
|---|---|
| 入力 | 上記1〜3の分岐条件（`winningRunId != run.Id`）が成立した状態。`winningRunId`は既に実行中の別Runの`RunId` |
| 出力 | `RunStartResult(rejectedRun, winningRunId)`。`rejectedRun`は`run`インスタンスに`Status="failed"`, `IsError=true`, `ResultSummary`に競合`RunId`（`winningRunId`）を含む文言, `FinishedAt=DateTimeOffset.UtcNow`を設定したもの（正確な文言は実装工程で確定してよい） |
| 副作用 | `rejectedRun`を`_runStore.SaveAsync`で保存する。**`_activeIssueRuns`へは一切書き込まない**（`GetOrAdd`が不成立だった時点で追加操作は発生しないため、明示的な解放処理も不要） |
| CLI起動 | 行わない |

##### 2.5.1-B 正常系（ロック獲得成功）の入出力

| 項目 | 内容 |
|---|---|
| 入力 | 上記1〜3の分岐条件（`winningRunId == run.Id`）が成立した状態 |
| 出力（対象ディレクトリ存在時） | `RunStartResult(run, null)`。`run`は`Status="running"`のまま（`ApplyResult`未実行、`ExecuteAsync`完了まで確定しない） |
| 出力（対象ディレクトリ不在時、境界値） | `RunStartResult(run, null)`。`run`は`Status="failed"`, `IsError=true`, `ResultSummary=$"対象ディレクトリが存在しません: {issue.TargetProjectPath}"`, `FinishedAt`設定済み（既存実装のメッセージ文言を維持）。**`ConflictingRunId`は`null`**（排他拒否とは無関係の失敗理由のため。REQ-12の`ConflictingRunId`は同時実行拒否時専用） |
| 副作用（対象ディレクトリ不在時） | `run`を`_runStore.SaveAsync`で保存（1回のみ） |
| 副作用（対象ディレクトリ存在時） | 下記「副作用（対象ディレクトリ存在時）の手順」のとおり、手順1〜6を順に実行する |
| ロック解放（`_activeIssueRuns`） | `backgroundStarted`が`true`になった場合は`ExecuteAsync`側の`finally`（2.5.3節）が解放を担当し、本メソッドの`finally`では何もしない。対象ディレクトリ不在等`backgroundStarted`到達前に早期returnした場合は、本メソッドの`finally`で`_activeIssueRuns.TryRemove(issue.Id, out _)`を実行する（component_design.md「ロック解放の設計方針」節参照。個別分岐ごとの解放コード追加は不要） |

###### 副作用（対象ディレクトリ存在時）の手順

上表「副作用（対象ディレクトリ存在時）」欄の詳細な処理順序は以下のとおり。

1. `MockRunGenerator.ShouldUseMock(configMockMode, _claudeCliPath)`（COMP-06）を呼び`run.IsMock`へ設定、`run.TriggeredByLoop = triggeredByLoop`を設定する。
2. その状態の`run`を`_runStore.SaveAsync`で保存する（1回のみ。component_design.md 232行目・本設計書2.2節「`IsMock`」行の「Run生成後・保存前に設定」と整合させ、`IsMock`/`TriggeredByLoop`未設定のまま`run`が永続化される中間状態を作らない）。
3. `RetentionPruner.PruneAsync(...)`（COMP-09）を呼び出す。
4. `_active[run.Id] = new RunContext(LogPathFor(run.Id))`へ登録する。
5. `_ = Task.Run(() => ExecuteAsync(run, issue, prompt, permissionMode, ctx, run.IsMock))`でバックグラウンド起動する。
6. 呼び出し成功直後に`backgroundStarted = true`を設定する。

**境界値・分岐条件**（テストケース設計への転用を想定）:

| # | 状況 | `winningRunId == run.Id`か | 対象ディレクトリ | 結果 |
|---|---|---|---|---|
| 1 | Issueへの初回・単独呼び出し | Yes | 存在 | 正常起動。`RunStartResult(run, null)`、CLI起動またはモック実行が開始される |
| 2 | Issueへの初回・単独呼び出し | Yes | 不在 | `RunStartResult(failedRun, null)`。CLI起動なし。`_activeIssueRuns`は`finally`で解放される |
| 3 | 同一Issueへ実行中に2回目の呼び出し | No | - | `RunStartResult(rejectedRun, winningRunId)`。CLI起動なし。既存の実行中Runには影響しない（REQ-12） |
| 4 | 同一Issueへほぼ同時に3件以上の呼び出しが到達 | 1件のみYes、残り全てNo | - | `GetOrAdd`はアトミックなため、勝者は必ずちょうど1件に定まる。敗者は全て#3と同じ扱い（勝者の`RunId`が共通の`winningRunId`として返る） |
| 5 | 1回目のRunが完了（成功/失敗/キャンセル問わず）した後、同一Issueへ再度呼び出し | Yes | - | `ExecuteAsync`の`finally`（2.5.3節）で既に`_activeIssueRuns`が解放済みのため、新規呼び出しは通常どおりロックを獲得できる（連続実行が阻害されない） |
| 6 | `_runStore.SaveAsync`のI/O例外・`RetentionPruner.PruneAsync`内の例外等、正常系のtry区間内で発生した予期しない例外 | Yes（区間には入った） | - | `backgroundStarted=false`のまま`finally`に到達し`_activeIssueRuns`を解放した上で、例外はそのまま呼び出し元へ再送出される（`catch`を設けない設計。component_design.md 258行目） |

#### 2.5.2 `ExecuteAsync`のモック分岐

```csharp
private async Task ExecuteAsync(
    Run run, Issue issue, string prompt, string permissionMode, RunContext ctx, bool isMock)
```

`isMock`は`StartAsync`側で既に確定済みの`run.IsMock`の値をそのまま渡す（`ShouldUseMock`の再計算はしない）。`MockRunGenerator.GenerateLines`（COMP-06）呼び出しに必要なStage情報は、`template`を新たな引数として受け取らず、`run.Stage`（`StartAsync`が`Run`構築時に`template.Stage`から複写済み）を使う（値は等価であり、引数を増やさない実装上の簡略化。component_design.mdの記載内容と矛盾しない）。

**`isMock`値ごとの入出力・副作用の違い**:

| 項目 | `isMock = false`（本番） | `isMock = true`（モック） |
|---|---|---|
| プロセス起動 | 既存どおり`ProcessStartInfo`構築〜`Process.Start`〜`PumpAsync`によるstdout/stderr読み取り〜`WaitForExitAsync`（変更なし） | 行わない |
| ログ生成元 | `claude` CLIサブプロセスの実際の標準出力（stream-json形式） | `MockRunGenerator.GenerateLines(run.Stage)`（COMP-06、純粋関数）が返す3行 |
| `ctx.Append`呼び出し | stdout/stderrの各行に対し既存どおり呼ぶ | `GenerateLines`が返す各行に対し同じ経路で呼ぶ（`ctx.Append`自体はSSE配信・ログファイル追記を伴う既存実装のまま） |
| `lastResultLine`の捕捉 | 既存どおり`line.Contains("\"type\":\"result\"")`で判定 | 同一判定ロジックをモック行に対しても適用（3行中1行が`type:"result"`） |
| `exitCode` | `process.ExitCode`（実プロセスの終了コード） | `0`固定（ローカル変数、実プロセスが存在しないため） |
| `ApplyResult(run, lastResultLine, exitCode)`呼び出し | 呼ぶ（2.5.3節のキャンセル判定でガードされる点は共通） | 同様に呼ぶ（同一コードパス、REQ-02の「共通経路」要件） |
| 対象プロジェクトへの実ファイル書き込み | claude CLI経由で発生しうる | 一切発生しない（REQ-02） |
| SSE配信・`_runStore.SaveAsync`・`ctx.Complete()`・`_active`/`_activeIssueRuns`からの除去・`RunCompleted`発火 | 共通（`isMock`の値によらず同一の`finally`を通る） | 同左 |

**境界値・分岐条件**:

| # | `isMock` | 備考 |
|---|---|---|
| 1 | `false` | 既存実装の経路をそのまま維持。回帰確認の基準ケース |
| 2 | `true` | `GenerateLines`が返す行数は常に3行（COMP-06側で確定済み）。3行全てに対し`ctx.Append`を呼ぶため、SSE購読側からは本番実行と区別できない配信形式になる（REQ-02） |

#### 2.5.3 `CancelAsync`と`ExecuteAsync`の`finally`の競合解消

**背景（既存バグ）**: component_design.md 274〜280行目参照（変更しない）。`CancelAsync`が保存する`Status="canceled"`を、`ExecuteAsync`の`finally`が`ApplyResult`の判定結果（`"failed"`等）で上書きしてしまう競合。

**`CancelAsync`側の変更点**:

```csharp
public async Task<bool> CancelAsync(string runId)
{
    if (!_active.TryGetValue(runId, out var ctx) || ctx.Process is null) return false;
    try
    {
        ctx.Process.Kill(entireProcessTree: true);
        lock (ctx._lock) { ctx.IsCanceled = true; }   // 追加: Kill成功と同時にフラグを立てる
    }
    catch (InvalidOperationException)
    {
        return false; // 既に終了している。IsCanceledは立てない
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
```

`RunContext`は`ClaudeRunEngine`の`private`なネスト型であり、そのメンバー（`_lock`含む）は外側の型`ClaudeRunEngine`のメソッドから直接アクセス可能（C#の入れ子型アクセス規則）である。そのため`CancelAsync`・`ExecuteAsync`はいずれも`RunContext`側に新規publicメソッドを追加せず、`lock (ctx._lock) { ... }`を直接記述できる。

**`ExecuteAsync`側の変更点**: `ApplyResult`呼び出しを`ctx.IsCanceled`でガードし、`finally`で最終的な`Status`を確定する。

```csharp
private async Task ExecuteAsync(Run run, Issue issue, string prompt, string permissionMode, RunContext ctx, bool isMock)
{
    string? lastResultLine = null;
    try
    {
        int exitCode;
        // isMockに応じて exitCode / lastResultLine を決定する処理（2.5.2節）

        run.ExitCode = exitCode;

        bool canceledBeforeApply;
        lock (ctx._lock) { canceledBeforeApply = ctx.IsCanceled; }
        if (!canceledBeforeApply)
        {
            ApplyResult(run, lastResultLine, exitCode);
        }
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
        bool wasCanceled;
        lock (ctx._lock) { wasCanceled = ctx.IsCanceled; }
        if (wasCanceled)
        {
            run.Status = "canceled";   // ApplyResult・catch側の設定に関わらず最終的に上書き
        }
        run.FinishedAt = DateTimeOffset.UtcNow;
        await _runStore.SaveAsync(run);
        ctx.Complete();
        _active.TryRemove(run.Id, out _);
        _activeIssueRuns.TryRemove(run.IssueId, out _);
        if (RunCompleted is not null) await RunCompleted.Invoke(run);
    }
}
```

`try`区間内の判定（`canceledBeforeApply`）は「`ApplyResult`を呼ばない」（component_design.md 278行目）を実現するためのガードである。一方、`finally`区間内の判定（`wasCanceled`）は「`run.Status`を明示的に`"canceled"`へ設定してから保存する」（同280行目）を実現するための、独立した2回目の判定である。両者は同じ`ctx.IsCanceled`を`lock (ctx._lock)`越しに読むが、`catch`ブロック経由で`try`側の判定を通らずに`finally`へ到達した場合（下表#5）にも`finally`側の判定が独立して機能するよう、あえて1回にまとめず2箇所で読む設計とする。

**入出力仕様**:

| 項目 | 内容 |
|---|---|
| 入力（`IsCanceled`の観測値） | `ctx.IsCanceled`（`CancelAsync`が`lock (ctx._lock)`越しに書き込んだ値を、`ExecuteAsync`が同じロック越しに読む） |
| 出力（最終的な`run.Status`） | `IsCanceled == true`なら常に`"canceled"`。`IsCanceled == false`なら`ApplyResult`の判定結果（`"succeeded"`／`"failed"`）または`catch`ブロックが設定する`"failed"` |
| 副作用 | `_runStore.SaveAsync(run)`（`finally`内、`CancelAsync`側の`SaveAsync`とは別インスタンス・別タイミングで実行されるが、最終的な永続化結果は一致する。保存順序に依存しない。component_design.md 280〜296行目のシーケンス図参照） |

**境界値・分岐条件**:

| # | 経路 | `finally`到達時の`ctx.IsCanceled` | `ApplyResult`呼び出し | 保存される`run.Status` |
|---|---|---|---|---|
| 1 | `try`が正常完了（`exitCode=0`, `IsError=false`） | `false` | 呼ぶ | `"succeeded"` |
| 2 | `try`が正常完了（`exitCode≠0`または`IsError=true`、キャンセル以外の失敗） | `false` | 呼ぶ | `"failed"` |
| 3 | `try`が正常完了（`CancelAsync`によるKillが原因で`exitCode≠0`） | `true` | 呼ばない（ガードでスキップ） | `"canceled"`（`finally`で明示設定） |
| 4 | `catch (Exception ex)`経路（CLI起動失敗等、キャンセルと無関係の実行時エラー） | `false` | 呼ばれない（`catch`が直接`Status="failed"`を設定済み） | `"failed"` |
| 5 | `catch (Exception ex)`経路かつ`CancelAsync`がほぼ同時に`IsCanceled`を立てたレアケース | `true` | 呼ばれない | `"canceled"`（`finally`が`catch`側の`"failed"`設定を上書き） |
| 6 | `CancelAsync`が呼ばれたが対象プロセスが既に終了していた（`InvalidOperationException`） | `false`（`IsCanceled`は立てられない） | 通常どおり呼ぶ | `ApplyResult`の判定結果どおり（通常は正常終了として`"succeeded"`または`"failed"`） |

#### 2.5.4 `RunContext`への`IsCanceled`フィールド追加

| 項目 | 内容 |
|---|---|
| 型 | `bool`（自動実装プロパティ、`{ get; set; }`） |
| 初期値 | `false`（既定値。C#の`bool`既定値をそのまま使い、コンストラクタでの明示初期化は不要） |
| 書き込み元 | `ClaudeRunEngine.CancelAsync`。`ctx.Process.Kill(entireProcessTree: true)`成功直後、同じ`lock (ctx._lock)`ブロック内で`true`に設定する（2.5.3節参照）。それ以外の書き込み元はない（`false`へ戻す経路は存在しない＝Runごとに使い捨ての`RunContext`インスタンスであるため再利用時のリセットを考慮する必要がない） |
| 読み取り元 | `ClaudeRunEngine.ExecuteAsync`。`try`区間内（`ApplyResult`呼び出し直前）と`finally`区間内（最終`Status`確定直前）の2箇所、いずれも`lock (ctx._lock)`越しに読む（2.5.3節参照） |
| スレッド間可視性 | `RunContext`が既に保持する`private readonly object _lock = new();`（`_lines`/`_completed`/`_signal`の保護に既存利用）を`IsCanceled`にも流用する。新設の`volatile`修飾子・別ロックは追加しない（component_design.md 298〜300行目の方針をそのまま踏襲） |

#### 2.5.5 `RunCompleted`イベント

```csharp
public event Func<Run, Task>? RunCompleted;
```

| 項目 | 内容 |
|---|---|
| 発火タイミング | `ExecuteAsync`の`finally`内、`_runStore.SaveAsync(run)`・`ctx.Complete()`・`_active.TryRemove(run.Id, out _)`・`_activeIssueRuns.TryRemove(run.IssueId, out _)`が全て完了した直後（2.5.3節のコード例、最終行）。すなわちRunの永続化・ロック解放が全て終わった後に発火するため、購読側が`RunCompleted`ハンドラ内で当該Issueへの新規`StartAsync`を呼んでも排他制御と矛盾しない |
| 引数 | `run`（`Models.Run`、`finally`到達時点で`Status`が最終確定した後のインスタンス）。手動キャンセルされたRunであれば2.5.3節の保証により`Status == "canceled"`が確定済み |
| 戻り値 | `Task`（購読側ハンドラが非同期処理を行えるようにするための型。`ExecuteAsync`側は`RunCompleted is not null`の場合に限り`await RunCompleted.Invoke(run)`する） |
| 購読者 | `LoopEngine`（COMP-08）。`HandleRunCompletedAsync`を`ClaudeRunEngine`インスタンス生成後に`+=`で購読する想定（購読タイミング・購読解除の要否はCOMP-08側の関数設計工程で確定する。本節では`ClaudeRunEngine`が「誰が購読しているか」を一切知らない疎結合設計であることのみを規定する） |
| 未購読時の挙動 | `RunCompleted`が`null`（誰も購読していない）の場合は何もしない。COMP-08未実装の段階でも`ClaudeRunEngine`単体として動作する |
| 例外時の扱い | 購読先ハンドラが例外を投げた場合の扱いはCOMP-08側の責務とする（`ClaudeRunEngine`側で`try/catch`はしない）。`ExecuteAsync`自体は`_ = Task.Run(...)`でfire-and-forget起動されているため、ここで未処理例外が発生した場合の扱いは既存実装からの変更点ではない |

対応ID: REQ-01, REQ-02, REQ-03, REQ-10, REQ-11, REQ-12, CON-05, CON-08

