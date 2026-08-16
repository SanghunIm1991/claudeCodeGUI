# 設計書: helloworld（PythonでHello Worldを出力するGUIアプリ）

## 0. 前提条件（要件定義書が存在しないための補完事項）

Issueの説明が「PythonでHelloworldを出力するGUIアプリを作ってください」のみで詳細仕様が未確定のため、以下を妥当な前提として設計を進める。実装後にユーザー確認が可能であれば、この前提の妥当性を確認すること。

| # | 項目 | 採用した前提 | 理由 |
|---|---|---|---|
| 1 | 言語/バージョン | Python 3.9以降（標準ライブラリのみ） | 追加インストール不要で最も可搬性が高い |
| 2 | GUIフレームワーク | `tkinter`（標準ライブラリ） | 別途パッケージ導入が不要。「Hello World」規模のアプリに対して妥当な最小構成 |
| 3 | 「出力」の解釈 | ウィンドウ内にラベルとして"Hello, World!"を表示する（コンソール出力ではない） | Issueが明示的に「GUIアプリ」を要求しているため |
| 4 | 追加操作 | ウィンドウを閉じるための標準の閉じるボタンのみ。追加ボタン等は無し | スコープの最小化（Issueタイトルが"helloworld"であり、過剰機能を避ける） |
| 5 | 対応OS | Windows（本プロジェクトの開発機環境）。tkinterはクロスプラットフォームのため他OSでも動作見込み | 環境メモ（CLAUDE.md）でPython 3.11/3.9双方でtkinter動作確認済みとの記載あり |
| 6 | 文字列の多言語対応 | 不要（"Hello, World!"固定） | スコープ外 |
| 7 | 自動テスト | ロジックとUI構築を分離し、ロジック部分は単体テスト可能な形にする | test-strategy方針（ロジック層とGUI層の分離）に準拠 |

## 1. コンポーネント設計

### 1.1 全体構成

シンプルなアプリケーションのため、レイヤーは以下の2つに限定する。

```
demo/
├── main.py            … エントリポイント（起動用スクリプト）
├── gui/
│   ├── __init__.py
│   └── app.py          … UIコンポーネント（HelloWorldAppクラス）
└── docs/
    └── design_helloworld.md   … 本設計書
```

### 1.2 コンポーネント一覧

| コンポーネント | 責務 | 依存先 |
|---|---|---|
| `main.py`（エントリポイント） | プロセス起動、`HelloWorldApp`の生成とイベントループ開始 | `gui.app` |
| `gui/app.py`（UI層） | ウィンドウ生成、ウィジェット配置、表示文言の取得 | `tkinter`（標準ライブラリ） |

### 1.3 コンポーネント間の関係

```
main.py
  └─ gui.app.HelloWorldApp  (tkinter.Tk のインスタンスを内部で生成)
       └─ gui.app.get_greeting_message()  ※表示文言を返す純粋関数として分離
```

- UI構築ロジック（ウィジェット配置）と表示文言生成ロジックを分離することで、文言生成部分を単体テスト可能にする（tkinterのイベントループを起動せずにテストできる）。
- 将来的に文言を設定ファイル化・多言語化する場合も、`get_greeting_message()`の内部実装のみを変更すればよい構成とする。

## 2. 関数設計

### 2.1 `main.py`

| 関数 | シグネチャ | 責務 | 入力 | 出力 |
|---|---|---|---|---|
| `main` | `def main() -> None` | アプリケーションの起動。`HelloWorldApp`を生成し`run()`を呼び出す | なし | なし |

エントリポイント条件:
```python
if __name__ == "__main__":
    main()
```

### 2.2 `gui/app.py`

| 関数/メソッド | シグネチャ | 責務 | 入力 | 出力 |
|---|---|---|---|---|
| `get_greeting_message` | `def get_greeting_message() -> str` | 表示する挨拶文字列を返す純粋関数 | なし | `"Hello, World!"` |
| `HelloWorldApp.__init__` | `def __init__(self) -> None` | `tkinter.Tk`を継承し、ウィンドウの基本設定（タイトル・サイズ）を行った上で`_build_widgets()`を呼び出す | なし | なし |
| `HelloWorldApp._build_widgets` | `def _build_widgets(self) -> None` | `get_greeting_message()`の戻り値を用いてラベルウィジェットを生成・配置する | なし | なし |
| `HelloWorldApp.run` | `def run(self) -> None` | `self.mainloop()`を呼び出しイベントループを開始する | なし | なし |

### 2.3 クラス設計詳細: `HelloWorldApp`

```python
class HelloWorldApp(tk.Tk):
    """Hello World を表示するメインウィンドウ。"""

    WINDOW_TITLE = "Hello World App"
    WINDOW_SIZE = "300x120"

    def __init__(self) -> None:
        super().__init__()
        self.title(self.WINDOW_TITLE)
        self.geometry(self.WINDOW_SIZE)
        self._build_widgets()

    def _build_widgets(self) -> None:
        label = tk.Label(self, text=get_greeting_message(), font=("Arial", 16))
        label.pack(expand=True)

    def run(self) -> None:
        self.mainloop()
```

- 継承元を`tk.Tk`とすることで、`HelloWorldApp`自体がウィンドウそのものとなり、呼び出し側（`main.py`）は生成して`run()`を呼ぶだけでよい薄いインターフェースになる。
- ウィジェット生成を`_build_widgets`に分離しているのは、将来ウィジェットが増えた場合にも`__init__`が肥大化しないようにするため（現時点ではラベル1つのみ）。

## 3. エラーハンドリング方針

- 本アプリは外部入出力・外部依存を持たないため、実行時例外が発生しうる箇所は事実上ない。
- `tkinter`が利用できない実行環境（tkinter未同梱のPythonビルド等）の場合は`ImportError`がそのまま送出される想定とし、独自の例外ハンドリングは追加しない（起きないはずの異常系に対する防御的コードを避ける方針）。

## 4. トレーサビリティ（Issueとの対応）

| Issue記述 | 対応する設計要素 |
|---|---|
| PythonでHelloworldを出力する | `get_greeting_message()` が `"Hello, World!"` を返す |
| GUIアプリ | `HelloWorldApp`（`tkinter.Tk`継承）がラベルとしてウィンドウに表示 |

## 5. 次工程（実装）への申し送り事項

- 実行コマンド想定: `py -3.11 main.py`（本PCの環境メモに従い、`python`コマンド単体ではなく`py`ランチャーでバージョンを明示すること）。
- 単体テスト対象は`get_greeting_message()`のみ（戻り値が`"Hello, World!"`であることを検証）。GUI部分（`HelloWorldApp`）の見た目確認はユーザーによる手動起動確認に委ねる。
