# トレーサビリティマトリックス

要件↔実装↔テストの対応関係を追跡するための表。1つの表に全工程を詰め込まず、隣接する工程どうしの対応表を4つ用意する。各表は上流工程のIDを行、下流工程のIDを列に置き、交点に「○」（③④は該当テストIDの列挙）を記載する。対応関係の具体的な根拠はここに書かず、各工程の成果物側にID単位で明記する。

## ①要件×コンポーネント

要件定義工程完了（2026-08-24）。コンポーネント設計工程完了（2026-08-24）。出典: `docs/01_requirements/requirements.md`、`docs/02_component_design/component_design.md`。

| REQ/NFR/CON ID | COMP-xx |
|---|---|
| REQ-01 | COMP-04, COMP-05, COMP-06 |
| REQ-02 | COMP-05, COMP-06 |
| REQ-03 | COMP-02, COMP-05 |
| CON-01 | COMP-04 |
| REQ-04 | COMP-15 |
| REQ-05 | COMP-15 |
| CON-02 | COMP-15 |
| CON-03 | COMP-15 |
| REQ-06 | COMP-04, COMP-07, COMP-11 |
| CON-04 | COMP-15 |
| NFR-01 | COMP-07 |
| REQ-07 | COMP-12 |
| REQ-08 | COMP-12 |
| REQ-09 | COMP-12 |
| REQ-27 | COMP-12 |
| CON-05 | COMP-05 |
| REQ-10 | COMP-05 |
| REQ-11 | COMP-05 |
| REQ-12 | COMP-05, COMP-11 |
| REQ-13 | COMP-13 |
| CON-06 | COMP-11, COMP-12, COMP-13 |
| REQ-14 | COMP-01, COMP-08 |
| REQ-15 | COMP-03, COMP-08, COMP-11, COMP-16 |
| REQ-16 | COMP-08 |
| REQ-17 | COMP-01, COMP-08, COMP-14 |
| REQ-18 | COMP-01, COMP-08, COMP-14 |
| REQ-19 | COMP-08, COMP-11, COMP-14 |
| REQ-20 | COMP-01, COMP-08, COMP-14 |
| REQ-21 | COMP-02, COMP-08 |
| CON-07 | COMP-08 |
| CON-08 | COMP-05, COMP-08 |
| REQ-22 | COMP-09 |
| REQ-23 | COMP-10 |
| REQ-24 | COMP-10 |
| REQ-25 | COMP-10 |
| NFR-02 | COMP-10 |
| NFR-03 | COMP-17 |
| NFR-04 | COMP-17 |
| REQ-26 | COMP-17 |
| CON-09 | COMP-17 |

## ②コンポーネント×関数

コンポーネント設計工程完了（2026-08-24）。出典: `docs/02_component_design/component_design.md`。関数列は関数設計工程で埋める。

| COMP-xx | FUNC-xx |
|---|---|
| COMP-01 Issue モデル拡張 | (関数なし。プロパティの読み書きはCOMP-08/COMP-11/COMP-14の関数設計側で規定) |
| COMP-02 Run モデル拡張 | (関数なし。プロパティの読み書きはCOMP-05/COMP-08の関数設計側で規定) |
| COMP-03 PromptTemplate モデル拡張 / TemplateSeeder変更 | `PromptTemplateDefaultResolver.ResolveDemotions`（新規・純粋関数）, `TemplateSeeder.SeedDefaultsAsync`（既存関数の変更）。`IsDefaultForStage`プロパティ自体はCOMP-01/02と同様、独立した関数を持たず読み書き元（COMP-03自身の`ResolveDemotions`、COMP-08の`ResolveDefaultTemplate`、COMP-16）に委譲 |
| COMP-04 appsettings.json 拡張 | (関数なし。設定値の読み取りはCOMP-05/COMP-06/COMP-07の関数設計側で規定。関数設計書2.4節参照) |
| COMP-05 ClaudeRunEngine 拡張 | `StartAsync`（シグネチャ変更）, `ExecuteAsync`（`isMock`引数追加・既存メソッドの変更）, `CancelAsync`（既存メソッドの変更）, `RunContext.IsCanceled`（既存ネスト型へのフィールド追加、独立関数なし）, `RunCompleted`イベント（新規） |
| COMP-06 MockRunGenerator（新規） | `ShouldUseMock`（新規・純粋関数）, `GenerateLines`（新規・純粋関数） |
| COMP-07 TargetPathValidator（新規） | `IsWithinAllowedRoots`（新規・純粋関数）, `IsAllowed`（新規・薄いラッパー） |
| COMP-08 LoopEngine（新規） | `Evaluate`（新規・静的純粋関数）, `GetNextStage`（新規・静的純粋関数）, `ResolveDefaultTemplate`（新規・静的純粋関数）, `HandleRunCompletedAsync`（新規・副作用あり）, `StartLoopAsync`（新規・副作用あり）, `StopLoopAsync`（新規・副作用あり） |
| COMP-09 RetentionPruner（新規） | `SelectRunsToPrune`（新規・静的純粋関数）, `PruneAsync`（新規・副作用あり） |
| COMP-10 OrphanSweepService（新規） | `OrphanDetection.Detect`（新規・静的純粋関数）, `OrphanSweepService.SweepAsync`（新規・副作用あり） |
| COMP-11 Program.cs エンドポイント拡張 | `IssueUpdateValidator.IsKnownPermissionMode`（新規・静的純粋関数）, `IssueUpdateValidator.IsKnownStage`（新規・静的純粋関数）, `ToRunStartResponse`（新規・共通ヘルパー）, `POST /api/issues`ハンドラ（変更）, `PUT /api/issues/{id}`ハンドラ（変更）, `POST /api/templates` / `PUT /api/templates/{id}`ハンドラ（変更）, `POST /api/issues/{issueId}/runs`ハンドラ（変更）, `POST /api/issues/{issueId}/loop/start`ハンドラ（新規）, `POST /api/runs/{id}/cancel`ハンドラ（変更） |
| COMP-12 SSE自動再接続・実行中Run検出（app.js） | `connectRunStream`（既存`startRun`からEventSource生成ロジックを集約・新設、副作用あり）, `selectIssue`（既存関数の変更・実行中Run検出ロジック追加）, `startRun`（既存関数のリファクタリング） |
| COMP-13 排他制御拒否時のUX誘導（app.js） | |
| COMP-14 自律ループ操作UI（app.js, styles.css） | |
| COMP-15 GUI配置の改善（app.js, styles.css） | |
| COMP-16 テンプレート既定フラグの編集UI（app.js, index.html） | |
| COMP-17 ClaudeCodeGui.Tests（新規プロジェクト） | |

## ③関数×テスト

| FUNC-xx | テストモジュール（該当テストID） |
|---|---|
| (関数設計完了後に追記) | |

## ④要件×テスト（直接検証トレース）

| REQ/NFR/CON ID | テストモジュール（該当テストID） |
|---|---|
| (テスト工程完了後に追記) | |

テスト工程完了時には、④の全要件ID行に最低1つのテストIDが記載されていることを確認し、網羅性チェックとする。
