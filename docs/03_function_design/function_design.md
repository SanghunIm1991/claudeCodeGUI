# claudeCodeGUI 関数設計書

## 0. 文書情報

| 項目 | 内容 |
|---|---|
| 版数 | 1.1 |
| 作成日 | 2026-08-25 |
| 改訂日 | 2026-08-30（コンポーネント単位ファイルへ分割） |
| 作成者（サブエージェント） | 関数設計工程 開発サブエージェント（論理設計担当） |
| 対象範囲 | `docs/02_component_design/component_design.md` COMP-01〜17（コンポーネント設計工程完了順に、コンポーネント単位で開発→レビューサイクルを回しながら追記する） |
| 入力ドキュメント | `docs/02_component_design/component_design.md`、`docs/01_requirements/requirements.md`、`docs/traceability_matrix.md`、`src/ClaudeCodeGui/` 実装コード |

## 1. 設計方針

- 各関数の入出力仕様は、そのままテストケース設計に転用できる粒度で記載する（CLAUDE.md品質方針）。具体的には、シグネチャ（引数・戻り値の型）、事前条件、事後条件・戻り値の意味、代表的な境界値・分岐条件を明記する。
- 副作用（ファイルI/O・DI経由のストアアクセス等）とロジック（純粋関数）を分離する方針を踏襲する（`component_design.md`の既存パターン、および`ArtifactService.ResolveWithinRoot`が体現するパターンに準拠）。純粋関数には「純粋関数（ストア等への副作用なし、単体テスト対象）」の注記を付け、副作用を伴う関数とは節を分けて記載する。
- 各関数には対応するコンポーネントID（COMP-xx）を明記し、コンポーネント設計書3節の当該コンポーネント節との整合性を保つ。コンポーネント設計書側で既に確定しているシグネチャ・判定順序・境界値（例: COMP-08の`Evaluate`判定順序、オフバイワン修正、ロック解放方針）はそのまま踏襲し、関数設計工程で内容を変更しない（変更が必要と判断した場合はレビュー指摘として扱う）。
- 各COMP-xxごとに`COMP-xx.md`（本ディレクトリ直下）として個別ファイルに分割する。単一ファイルへの追記運用は、ファイル肥大化に伴うサブエージェント呼び出し1回あたりのトークン消費増大・挿入位置の取り違え（構造的ミス）のリスクを高めるため、2026-08-30にコンポーネント単位ファイルへ分割した（経緯は`docs/qa_log.md`参照）。コンポーネント設計工程完了順（COMP-01から）に1コンポーネント＝1ファイル＝1つの開発→レビューサイクルとして作成していく（`waterfall-dev-workflow`スキル自律ループモード運用）。
- 振る舞いを持たないデータ構造（モデルクラスの単純なプロパティ追加等）で独立した関数が不要と判断した場合は、その旨と根拠を明記し、当該プロパティを読み書きする利用側コンポーネントの関数設計にその責務を委譲する。
- ID対応関係の一次情報源は`docs/traceability_matrix.md`とする。各コンポーネントファイルの末尾に簡潔な対応ID一覧のみを記載し、非自明な対応関係のみ個別に理由を補足する。

## 2. コンポーネント別 関数設計（索引）

各コンポーネントの詳細は個別ファイル（`COMP-xx.md`）を参照。

| コンポーネント | 対象ファイル | 状態 |
|---|---|---|
| [COMP-01](COMP-01.md) `Issue` モデル拡張 | `src/ClaudeCodeGui/Models/Issue.cs` 他 | 完了 |
| [COMP-02](COMP-02.md) `Run` モデル拡張 | `src/ClaudeCodeGui/Models/Run.cs` 他 | 完了 |
| [COMP-03](COMP-03.md) `PromptTemplate` モデル拡張 / `TemplateSeeder` 変更 | `src/ClaudeCodeGui/Models/PromptTemplate.cs` 他 | 完了 |
| [COMP-04](COMP-04.md) `appsettings.json` 拡張 | `src/ClaudeCodeGui/appsettings.json` 他 | 完了 |
| [COMP-05](COMP-05.md) `ClaudeRunEngine` 拡張 | `src/ClaudeCodeGui/Services/ClaudeRunEngine.cs` | 完了 |
| [COMP-06](COMP-06.md) `MockRunGenerator`（新規） | `src/ClaudeCodeGui/Services/MockRunGenerator.cs` | 完了 |
| [COMP-07](COMP-07.md) `TargetPathValidator`（新規） | `src/ClaudeCodeGui/Services/TargetPathValidator.cs` | 完了 |
| [COMP-08](COMP-08.md) `LoopEngine`（新規） | `src/ClaudeCodeGui/Services/LoopEngine.cs` | 完了 |
| [COMP-09](COMP-09.md) `RetentionPruner`（新規） | `src/ClaudeCodeGui/Services/RetentionPruner.cs` | 完了 |
| [COMP-10](COMP-10.md) `OrphanSweepService`（新規） | `src/ClaudeCodeGui/Services/OrphanSweepService.cs` | 完了 |
| [COMP-11](COMP-11.md) `Program.cs` エンドポイント拡張・DI配線・起動時処理 | `src/ClaudeCodeGui/Program.cs` | 完了 |
| [COMP-12](COMP-12.md) SSE自動再接続・実行中Run検出 | `src/ClaudeCodeGui/wwwroot/app.js` | 完了 |
| [COMP-13](COMP-13.md) 排他制御拒否時のUX誘導 | `src/ClaudeCodeGui/wwwroot/app.js` | 完了 |
| [COMP-14](COMP-14.md) 自律ループ操作UI | `src/ClaudeCodeGui/wwwroot/app.js`, `styles.css` | 完了 |
| [COMP-15](COMP-15.md) GUI配置の改善 | `src/ClaudeCodeGui/wwwroot/app.js`, `styles.css` | 完了 |
| [COMP-16](COMP-16.md) テンプレート既定フラグの編集UI | `wwwroot/app.js`, `wwwroot/index.html` | 完了 |
| [COMP-17](COMP-17.md) `ClaudeCodeGui.Tests`（新規プロジェクト） | `src/ClaudeCodeGui.Tests/` | 完了 |
