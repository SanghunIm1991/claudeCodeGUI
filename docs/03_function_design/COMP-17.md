# COMP-17 `ClaudeCodeGui.Tests`（新規プロジェクト）

**対象ファイル**: `src/ClaudeCodeGui.Tests/`（新規プロジェクト一式）

**責務**（component_design.md 698〜730行目、変更しない）: xUnitベースの単体・結合テストプロジェクトを追加する（REQ-26）。`test-strategy`スキルの3層構成（単体/結合/GUI）のうち、単体・結合の2層を自動テストとして整備する（GUIはCON-09により対象外）。プロジェクト構成（`Unit/`・`Integration/`の各ファイルと対象関数の対応）・単体テスト方針（NFR-03）・結合テスト方針（NFR-04）はcomponent_design.md 706〜728行目で確定済みであり、本節はこれをそのまま踏襲した上で、関数設計工程として必要な「プロジェクトの配線」レベルの詳細（csproj構成、テスト用一時ディレクトリの実現方法、`Program`クラスのアクセシビリティ等）を具体化する。

## 2.17.0 本コンポーネントの役割とテスト工程（`docs/04_test/`）との境界

**COMP-01〜16との性質の違い**: COMP-01〜16はいずれも既存実装コード（`src/ClaudeCodeGui/`）への機能追加・拡張であり、関数設計工程の成果物は「その機能を実現する関数の入出力仕様」であった。COMP-17は逆に「その機能群を検証するテスト自動化基盤」そのものであり、本節が確定させるのは以下の2点に限定される。

1. `src/ClaudeCodeGui.Tests/`という新規プロジェクトの構成（csproj設定・フォルダ構成）
2. 各テストファイルが対象とする実装関数・エンドポイントの対応関係、および単体/結合の区分

**本節が扱わない事項（テスト工程`docs/04_test/`の役割）**: 個々のテストケースの詳細（具体的な入力値・期待値・テストID・テストデータの由来管理）は、本節では確定させない。これらはCOMP-05〜16の各関数設計書（COMP-05.md〜COMP-16.mdの「境界値・分岐条件」表）に既にテストケース設計へ転用可能な粒度で記載済みであり、`docs/04_test/`のテスト仕様書がこれらを参照してテストID（REQ-xx/FUNC-xx起点）を付与し、`test-strategy`スキルが定める列（テストID・対応要件ID・対応関数ID・テストレベル・由来）を持つ一覧として正式化する。本節の各テストファイルは「入れ物」の確定に留め、中身のテストメソッド名・Theory/InlineDataの具体的な値までは規定しない。

## 2.17.1 `ClaudeCodeGui.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ClaudeCodeGui\ClaudeCodeGui.csproj" />
  </ItemGroup>

</Project>
```

| 項目 | 内容 | 根拠 |
|---|---|---|
| SDK種別 | `Microsoft.NET.Sdk`（`Microsoft.NET.Sdk.Web`ではない） | 単体・結合テストプロジェクト自体はWebアプリではない。`Microsoft.AspNetCore.Mvc.Testing`パッケージが`WebApplicationFactory<T>`利用に必要なASP.NET Core共有フレームワークへの参照をNuGetメタデータ経由で解決するため、`Sdk.Web`への切り替えや`FrameworkReference`の追加は不要（ASP.NET Core公式の結合テスト構成パターンと同一） |
| `TargetFramework` | `net9.0` | `src/ClaudeCodeGui/ClaudeCodeGui.csproj`（1〜9行目）と同一。異なるターゲットフレームワークでは`ProjectReference`が正しくビルドできない |
| パッケージバージョン | メジャーバージョンのみ固定（`17.*`/`2.*`/`9.0.*`） | 正確なパッチバージョンは実装工程で`dotnet add package`実行時点の最新安定版を採用する（COMP-05 2.5.1節「正確な文言は実装工程で確定してよい」と同種の、実装時点で確定すべき詳細の据え置き） |
| `IsPackable` | `false` | テスト専用プロジェクトのためNuGetパッケージ化は不要（標準的なテストプロジェクトの既定設定） |
| `ProjectReference` | `src/ClaudeCodeGui/ClaudeCodeGui.csproj`への相対参照 | Unit/Integration双方のテストが`ClaudeCodeGui`名前空間の型（`Services/`配下の各クラス、`Program`）を直接参照するため必須 |

**フォルダ構成**（component_design.md 706〜722行目のとおり、以下の階層で作成する）:

```
src/ClaudeCodeGui.Tests/
  ClaudeCodeGui.Tests.csproj
  Unit/
    ClaudeRunEngineTests.cs
    MockRunGeneratorTests.cs
    TargetPathValidatorTests.cs
    LoopEngineTests.cs
    RetentionPrunerTests.cs
    OrphanDetectionTests.cs
    ArtifactServiceTests.cs
    PromptTemplateDefaultResolverTests.cs     ← 2.17.5節「発見した追加対象」
    IssueUpdateValidatorTests.cs              ← 同上
  Integration/
    ClaudeCodeGuiWebApplicationFactory.cs     ← 2.17.8節（共通テスト基盤、新規）
    IssueEndpointsTests.cs
    RunEndpointsTests.cs
    LoopEndpointsTests.cs
    TemplateEndpointsTests.cs
```

**テスト実行標準形**: `dotnet test src\ClaudeCodeGui.Tests\ClaudeCodeGui.Tests.csproj`（CLAUDE.md「実装環境」章）。

## 2.17.2 discovered issue: `WebApplicationFactory<Program>`のための`Program`クラスのアクセシビリティ

**発見した確認事項**: `src/ClaudeCodeGui/Program.cs`（1〜194行目）を確認したところ、トップレベルステートメント形式で記述されており、C#コンパイラが自動生成する`Program`クラスは既定で`internal`アクセシビリティになる（.NET標準仕様）。`WebApplicationFactory<Program>`（`Microsoft.AspNetCore.Mvc.Testing`）は型引数`Program`を別アセンブリ（`ClaudeCodeGui.Tests`）から参照する必要があるため、このままでは`ClaudeCodeGuiWebApplicationFactory : WebApplicationFactory<Program>`（2.17.8節）がコンパイルエラーになる。

**対応（実装工程での必須の軽微な変更）**: `src/ClaudeCodeGui/Program.cs`の末尾（194行目、既存の`record`定義群の後）に以下の1行を追加する。

```csharp
public partial class Program { }
```

これはASP.NET Core公式の結合テスト構成パターン（トップレベルステートメント形式のWebアプリを`WebApplicationFactory<Program>`でテストする際の標準的な対応）であり、`Program`クラスの既存の動作（コンパイラ生成の`partial`クラスへメンバーを追加せず、空の`public`宣言を重ねるだけ）には一切影響しない。COMP-11（`Program.cs`、完了済み）の確定済み内容（DI登録・エンドポイント定義・起動時処理）を変更するものではないため、COMP-11の関数設計をやり直す必要はないが、COMP-17（本コンポーネント）の実装着手時に`Program.cs`側へこの1行を追加する必要がある点を申し送る。

## 2.17.3 discovered issue: テスト用`runtime-data`一時ディレクトリの分離方法

**現状の実装の制約**: `Program.cs` 7行目は`var dataRoot = Path.Combine(builder.Environment.ContentRootPath, "runtime-data");`であり、`JsonFileStore<Issue>`/`JsonFileStore<PromptTemplate>`/`JsonFileStore<Run>`（10〜12行目）・`ClaudeRunEngine`のログ格納先（COMP-05）・`OrphanSweepService`の各パス（COMP-10 2.10.0節）は、いずれもこの単一の`dataRoot`から導出される。設定キー経由で`dataRoot`を差し替える手段は現状存在しない。

`WebApplicationFactory<Program>`は既定では`ContentRootPath`をテストプロジェクトのビルド出力ディレクトリ等から解決するため、`ContentRootPath`を変更せずに`dataRoot`だけをテスト用の一時ディレクトリへ差し替えることができない。`UseContentRoot(tempDir)`で`ContentRootPath`自体を差し替える方法も検討したが、その場合`appsettings.json`・`wwwroot`の解決も同時に一時ディレクトリ基準へ変わってしまい、`ClaudeCli:MockMode`等の設定上書き（2.17.8節）との組み合わせが複雑になる。

**対応（実装工程での軽量な変更を提案）**: `Program.cs` 7行目を以下のとおり変更し、設定キー`DataRoot`が指定されていればそれを優先する。

```csharp
var dataRoot = builder.Configuration["DataRoot"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "runtime-data");
```

`appsettings.json`に`DataRoot`キーは追加しない（未設定時は既存どおり`ContentRootPath`基準のパスとなり、本番動作に一切影響しない）。結合テスト側は`ClaudeCodeGuiWebApplicationFactory`（2.17.8節）が`ConfigureAppConfiguration`でこの`DataRoot`キーにテスト用一時ディレクトリの絶対パスを注入することで、`Issue`/`PromptTemplate`/`Run`の各ストア・ログファイル・孤児Run退避先を含む`runtime-data`配下の全データを実データ（`src/ClaudeCodeGui/runtime-data/`）と完全に分離できる。

この変更はCLAUDE.md品質方針が求める「軽量な型・入力ガード等の実コード修正をデフォルトの対応として提案する」に沿った、既存動作を変えない後方互換の1行差分であり、COMP-11の確定済みDI登録・エンドポイント定義への変更は伴わない（`dataRoot`の値を後続で参照する`Program.cs`10〜14・28〜35行目のコードはそのまま）。

## 2.17.4 discovered gap: 追加すべき単体テストファイル

component_design.md 706〜722行目が確定させたUnit/配下7ファイルの単体テスト方針（724行目）は「COMP-05〜COMP-10で切り出した静的・純粋関数」と明記しているが、これはCOMP-17自体の当初のコンポーネント設計時点（COMP-11着手前）を反映したものであり、その後確定したCOMP-03・COMP-11にも同種の静的純粋関数が存在することを確認した。

| 発見した関数 | 所属 | 性質 | 対応 |
|---|---|---|---|
| `PromptTemplateDefaultResolver.ResolveDemotions(IReadOnlyList<PromptTemplate> allTemplates, PromptTemplate candidate)` | COMP-03 2.3.2節 | 純粋関数（ストア等への副作用なし、NFR-03明記） | `Unit/PromptTemplateDefaultResolverTests.cs`を新規追加 |
| `IssueUpdateValidator.IsKnownStage(string? stage)` / `IsKnownPermissionMode(string? mode)` | COMP-11 2.11.3節 | 純粋関数（同上、NFR-03明記） | `Unit/IssueUpdateValidatorTests.cs`を新規追加 |

いずれもNFR-03「静的・純粋関数を優先してカバーする」という単体テスト方針そのものが要求する対象であり、component_design.mdの確定内容（テストプロジェクトが単体・結合の2層を整備するという基本構成）を変更するものではなく、対象関数一覧を実際に完了済みのCOMP-03・COMP-11の内容と整合させる補完である。境界値・分岐条件はそれぞれCOMP-03 2.3.2節「代表的な境界値・分岐条件」表、COMP-11 2.11.3節「代表的な境界値・分岐条件」表（#1〜8）にテストケース設計へ転用可能な粒度で既に記載済みであり、本節では重複記載しない。

**追加しない判断をした関数（参考）**: `TemplateSeeder.SeedDefaultsAsync`（COMP-03 2.3.3節、既存関数の変更）は副作用（`store.GetAllAsync`/`SaveAsync`）を伴い純粋関数ではないため、NFR-03の単体テスト優先対象には該当しない。この関数は`Program.cs`起動シーケンス内で必ず呼ばれるため、Integration/配下の全テストファイル（2.17.7節）が結合テストのアプリ起動を通じて間接的に検証する（各Stageの既定テンプレートが5件seedされることは、`TemplateEndpointsTests.cs`・`LoopEndpointsTests.cs`双方の前提条件として自然にカバーされる）。独立した単体テストファイルは追加しない。

`ToRunStartResponse`（COMP-11 2.11.7節）はローカル関数（`Program.cs`内で`static IResult ToRunStartResponse(...)`として定義）であり、`ClaudeCodeGui.Tests`側からは直接参照できない（C#のローカル関数は宣言元スコープの外から呼び出し不能）。したがって単体テスト対象にはできず、`Integration/RunEndpointsTests.cs`・`Integration/LoopEndpointsTests.cs`がHTTPレスポンス（`202 Accepted`/`409 Conflict`）を通じて間接的に検証する（2.17.7節）。

## 2.17.5 Unit/配下 各ファイルの対象関数一覧

| ファイル | 対象関数 | 対応COMP-xx | 備考 |
|---|---|---|---|
| `ClaudeRunEngineTests.cs` | `ClaudeRunEngine`インスタンスに対する2つの`StartAsync`のほぼ同時呼び出し（排他制御、`_activeIssueRuns.GetOrAdd`のアトミック性を検証） | COMP-05 2.5.1節 | component_design.md 710行目のとおり、排他判定自体は静的な純粋関数へ切り出されていないため、`ClaudeRunEngine`インスタンスを介した準結合テストとして書く（純粋な単体テストではない点に注意） |
| `MockRunGeneratorTests.cs` | `MockRunGenerator.ShouldUseMock(bool, string)`, `MockRunGenerator.GenerateLines(string)` | COMP-06 2.6.1, 2.6.2節 | 真の純粋関数。COMP-06の境界値表（#1〜8、#1〜5）をそのままテストケースの入力候補として使える |
| `TargetPathValidatorTests.cs` | `TargetPathValidator.IsWithinAllowedRoots(string, IReadOnlyList<string>)` | COMP-07 2.7.2節 | `IsAllowed`（薄いラッパー）自体の追加テストは「コンストラクタに渡した値がそのまま静的関数へ渡ること」の確認に留める（COMP-07 2.7.3節） |
| `LoopEngineTests.cs` | `LoopEngine.Evaluate(...)`, `LoopEngine.GetNextStage(string)`, `LoopEngine.ResolveDefaultTemplate(...)` | COMP-08 2.8.1〜2.8.3節 | `HandleRunCompletedAsync`/`StartLoopAsync`/`StopLoopAsync`（副作用あり）はここでは対象外。結合テスト側（`LoopEndpointsTests.cs`）でHTTP経由の一連の流れとして検証する |
| `RetentionPrunerTests.cs` | `RetentionPruner.SelectRunsToPrune(IReadOnlyList<Run>, int)` | COMP-09 2.9.1節 | `PruneAsync`（副作用あり）は対象外 |
| `OrphanDetectionTests.cs` | `OrphanDetection.Detect(IReadOnlyList<Run>, IReadOnlySet<string>?, double)` | COMP-10 2.10.1節 | 安全弁ケース（`IssueStoreReadFailed`・`HighOrphanRatio`）を含む。`SweepAsync`（副作用あり）は対象外 |
| `ArtifactServiceTests.cs` | `ArtifactService.List`/`ReadFile`/`WriteFile`（`ResolveWithinRoot`は`private static`のため間接検証） | 既存ロジック（2.17.6節参照） | 2.17.6節参照 |
| `PromptTemplateDefaultResolverTests.cs`（新規追加） | `PromptTemplateDefaultResolver.ResolveDemotions(...)` | COMP-03 2.3.2節 | 2.17.4節「discovered gap」参照 |
| `IssueUpdateValidatorTests.cs`（新規追加） | `IssueUpdateValidator.IsKnownStage(string?)`, `IsKnownPermissionMode(string?)` | COMP-11 2.11.3節 | 同上 |

## 2.17.6 `ArtifactServiceTests.cs`: `private static`メソッドの単体テスト方法

**確認事項**: `src/ClaudeCodeGui/Services/ArtifactService.cs`（49〜58行目）を確認したところ、`ResolveWithinRoot(string rootPath, string relativePath)`は`private static`である。component_design.md・COMP-17節が「`ResolveWithinRoot`（既存ロジック、未整備だったため追加）」と記載しているが、`private`メンバーは`ClaudeCodeGui.Tests`アセンブリから直接呼び出すことができない（リフレクションを用いれば技術的には可能だが、他の全単体テスト対象――`ShouldUseMock`・`IsWithinAllowedRoots`・`Evaluate`等――がいずれも`public static`である本プロジェクトの一貫したテスト容易性方針と整合しない）。

**対応**: `ResolveWithinRoot`を直接呼び出すのではなく、これを内部で使用する`public`メソッド`ArtifactService.List(string rootPath, string relativeDir)`・`ReadFile(string rootPath, string relativePath)`・`WriteFile(string rootPath, string relativePath, string content)`（`ArtifactService.cs` 11〜40行目）を経由して間接的に検証する。具体的には以下の観点をテストケースとして設計する（詳細な入力値・期待値の確定は`docs/04_test/`の役割、2.17.0節）。

| # | 検証観点 | 期待される挙動 |
|---|---|---|
| 1 | `relativePath`が`rootPath`配下を指す通常のパス | 正常に処理される（`List`は`ArtifactEntry`一覧、`ReadFile`/`WriteFile`は例外なし） |
| 2 | `relativePath`が`../`等で`rootPath`の外を指すパス（ディレクトリトラバーサル） | `UnauthorizedAccessException`が送出される（`ResolveWithinRoot`49〜58行目、COMP-07 2.7.2節「`../`を含む相対パス表記の扱い」と同種の`Path.GetFullPath`正規化を利用） |
| 3 | `relativePath`が`rootPath`自身（空文字列・`"."`等） | 正常に処理される（`combined == root`の場合は例外を投げない分岐、53行目） |
| 4 | `WriteFile`で新規サブディレクトリを含むパスへ書き込む | `Directory.CreateDirectory`（38行目）により親ディレクトリが自動作成された上でファイルが書き込まれる |

この間接検証方針自体は、`TargetPathValidator.IsAllowed`（薄いラッパー、COMP-07 2.7.3節）を静的関数`IsWithinAllowedRoots`側のテストに集約し`IsAllowed`自体は「値の受け渡しの確認に留める」とした考え方と対称的である（`ArtifactService`の場合は逆に、正規化ロジック本体が`private`であるため`public`側からしか到達できない点が異なる）。

**将来`ResolveWithinRoot`単体のテストが必要になった場合の選択肢（参考、対応不要）**: `internal`へアクセシビリティを変更し`[assembly: InternalsVisibleTo("ClaudeCodeGui.Tests")]`を追加する方法もあるが、現状の`List`/`ReadFile`/`WriteFile`経由の間接検証で正規化ロジックの分岐は全て到達可能（上表#1〜3）であるため、本節では既存の`private`のままとし、アクセシビリティ変更は行わない。

## 2.17.7 Integration/配下 各ファイルの対象エンドポイント一覧

**結合テスト方針（NFR-04、component_design.md 726行目）**: `WebApplicationFactory<Program>`を用い、`appsettings`で`ClaudeCli:MockMode=true`を指定した実インスタンスに対してHTTPリクエストを送る。`ClaudeRunEngine`・`ArtifactService`・`JsonFileStore<T>`はモックに置き換えず実際の呼び出し関係のまま検証する。具体的な実現方法は2.17.8節。

| ファイル | 対象エンドポイント | 対応COMP-xx | 検証の要点 |
|---|---|---|---|
| `IssueEndpointsTests.cs` | `POST /api/issues`, `PUT /api/issues/{id}`, `GET /api/issues`, `GET /api/issues/{id}`, `DELETE /api/issues/{id}` | COMP-11 2.11.4, 2.11.5, 2.11.11節 | `TargetPathValidator.IsAllowed`によるパス許可判定（400）、`IssueUpdateValidator`による`CurrentStage`/`DefaultPermissionMode`検証（400）、検証順序（2.11.5節フロー図）を含むCRUD一連の流れ |
| `RunEndpointsTests.cs` | `POST /api/issues/{issueId}/runs`, `GET /api/runs/{id}`, `POST /api/runs/{id}/cancel`, `GET /api/issues/{issueId}/runs` | COMP-11 2.11.8, 2.11.10節、COMP-05 | モックモード実行での`202 Accepted`、同一Issueへの同時実行時の`409 Conflict`（排他拒否、`ToRunStartResponse`経由で間接検証）、`cancel`成功時の`LoopEngine.StopLoopAsync`連携（`run is null`の境界値含む） |
| `LoopEndpointsTests.cs` | `POST /api/issues/{issueId}/loop/start`, ループ経由の自動遷移（`RunCompleted`購読、COMP-11 2.11.2節の起動時処理により配線済み）, `POST /api/runs/{id}/cancel`によるループ停止 | COMP-11 2.11.9節、COMP-08 | ループ開始→（モック実行の高速な完了を利用した）自動遷移→手動停止の一連。`TemplateSeeder.SeedDefaultsAsync`が起動時に5Stage分の既定テンプレートをseedする（COMP-03 2.3.3節）ため、テスト側で個別に既定テンプレートを用意する前処理は不要 |
| `TemplateEndpointsTests.cs` | `POST /api/templates`, `PUT /api/templates/{id}`, `GET /api/templates`, `GET /api/templates/{id}`, `DELETE /api/templates/{id}` | COMP-11 2.11.6節、COMP-03 | `IsDefaultForStage=true`保存時の`PromptTemplateDefaultResolver.ResolveDemotions`による既存既定テンプレートの降格（後勝ち方式）、既定0件データ不整合を作らないための一意性維持 |

## 2.17.8 結合テスト共通基盤: `ClaudeCodeGuiWebApplicationFactory`

2.17.2節（`Program`クラスのアクセシビリティ）・2.17.3節（`DataRoot`分離）・上記結合テスト方針（`ClaudeCli:MockMode=true`固定）を満たすため、4つのIntegrationテストファイルが共通して使うカスタムファクトリを1つ用意する（xUnitの`IClassFixture<T>`として各テストクラスが利用する想定。具体的な継承・コンストラクタ注入の型付けは実装工程で確定してよい）。

```csharp
public class ClaudeCodeGuiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _tempDataRoot =
        Path.Combine(Path.GetTempPath(), "claudeCodeGui-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ClaudeCli:MockMode"] = "true",
                ["DataRoot"] = _tempDataRoot,
            });
        });
    }

    public Task InitializeAsync() => Task.CompletedTask; // ディレクトリ自体はProgram.cs起動時にDirectory.CreateDirectory(dataRoot)で作成される

    public new Task DisposeAsync()
    {
        if (Directory.Exists(_tempDataRoot))
        {
            Directory.Delete(_tempDataRoot, recursive: true);
        }
        return base.DisposeAsync().AsTask();
    }
}
```

| 設計判断 | 内容 |
|---|---|
| `ClaudeCli:MockMode=true`の設定方法 | `appsettings.Testing.json`等の新規設定ファイルを追加するのではなく`ConfigureAppConfiguration`での`AddInMemoryCollection`を用いる。設定ファイルの追加はデプロイ物へ混入するリスクがあり、インメモリ設定なら`ClaudeCodeGui.Tests`プロジェクト内に閉じる |
| `DataRoot`の一時ディレクトリ生成タイミング | `ClaudeCodeGuiWebApplicationFactory`インスタンスごと（xUnitでは`IClassFixture<T>`によりテストクラス単位）に`Guid.NewGuid()`でユニークなパスを払い出す。テストクラス間の干渉を防ぐと同時に、同一クラス内の複数`[Fact]`/`[Theory]`は同一の一時ディレクトリ・同一のアプリインスタンスを共有する（xUnitの`IClassFixture`の標準的なライフサイクル） |
| クリーンアップ | `DisposeAsync`でテスト用一時ディレクトリを再帰削除する。実データ（`src/ClaudeCodeGui/runtime-data/`）には一切触れない設計（2.17.3節の`DataRoot`分離により物理的に別ディレクトリ） |
| `Security:AllowedProjectRoots`の扱い | 既定では設定しない（COMP-04・COMP-07の「空＝制限なし」仕様により、大半のテストではパス制限を意識しなくてよい）。`IssueEndpointsTests.cs`でREQ-06（許可ルート外の拒否）を検証する`[Fact]`に限り、個別に`WithWebHostBuilder`でこの設定を上書きした専用のファクトリインスタンスを使う |
| `TemplateSeeder`・`OrphanSweepService.SweepAsync`の起動時実行 | `DataRoot`が空の新規ディレクトリであるため、`SeedDefaultsAsync`は5件の既定テンプレートをseedし（COMP-03 2.3.3節#1）、`SweepAsync`は`allRuns`が空のため何もせず即終了する（COMP-10 2.10.1節#1相当）。いずれもテスト実行を妨げない |

## 2.17.9 単体テスト方針・結合テスト方針の確認まとめ

component_design.md 724〜728行目が確定させた単体テスト方針（NFR-03）・結合テスト方針（NFR-04）・GUIテスト方針（CON-09）は、上記2.17.4節「discovered gap」による2ファイルの追加を除き、内容の変更なくそのまま踏襲する。GUIテストは引き続き自動テストとして整備せず、正常系のスクリーンショット確認は実装完了後にAIエージェントへ別途依頼する運用とする（component_design.md 728行目、変更なし）。

対応ID: NFR-03, NFR-04, REQ-26, CON-09
