# COMP-16 テンプレート既定フラグの編集UI（`app.js`, `index.html`）

**対象ファイル**: `src/ClaudeCodeGui/wwwroot/app.js`, `src/ClaudeCodeGui/wwwroot/index.html`

**責務**（component_design.md 690〜696行目）: `PromptTemplate.IsDefaultForStage`（COMP-03 2.3.1節）をGUIから設定できるようにする。テンプレート作成フォーム（`index.html`の`#template-form`）と編集フォーム（`app.js`の`renderTemplateDetail`が生成する`#template-edit-form`）の両方に「この工程の既定テンプレートにする」チェックボックスを追加し、`POST`/`PUT /api/templates`のペイロードに含める。

| 機能 | 対応要件 | 実装方法 |
|---|---|---|
| 作成フォームへの既定フラグチェックボックス追加 | REQ-15 | `#template-form`（index.html、既存46〜61行目）に`#template-is-default`チェックボックスを追加し、既存のPOST送信ハンドラ（app.js、既存373〜385行目）のペイロードへ`isDefaultForStage`を含める |
| 編集フォームへの既定フラグチェックボックス追加 | REQ-15 | `renderTemplateDetail`（app.js、既存338〜371行目）が生成する`#template-edit-form`に`#te-is-default`チェックボックスを追加し、現在値（`t.isDefaultForStage`）を`checked`属性へ反映する。既存のPUT送信ハンドラ（同352〜363行目）のペイロードへ`isDefaultForStage`を含める |

一意性解決（同一Stageで別テンプレートに既にチェックが入っている場合に降格する処理）自体は`PromptTemplateDefaultResolver.ResolveDemotions`（COMP-03 2.3.2節）が持ち、COMP-11のPOST/PUTハンドラ（2.11.6節）がその結果を保存する。本コンポーネントはチェックボックスの現在値をペイロードへ渡すのみで、一意性解決ロジックの再設計・重複記載は行わない。

**事前条件**:

- `index.html`の`#template-form`（既存46〜61行目、`#template-stage`・`#template-name`・`#template-body`・送信ボタンで構成）が現状の記述のままであること。
- `app.js`の`renderTemplateDetail`（既存338〜371行目）が`#template-edit-form`（342〜350行目、`#te-stage`・`#te-name`・`#te-body`・送信ボタン群で構成）を`innerHTML`で構築し、直後（352〜370行目）で`#template-edit-form`のsubmitイベント・`#te-delete`のclickイベントを登録済みであること。
- COMP-11（2.11.6節）が確定済みの`SaveTemplateRequest(string Name, string Stage, string Body, bool IsDefaultForStage)`（POST/PUT共通のリクエストDTO）が実装済みであること。`IsDefaultForStage`は非nullable `bool`であり、リクエストJSONに当該キーが存在しない場合`System.Text.Json`は既定値`false`として黙って埋める（COMP-14 2.14.1節「discovered issue」と同種の`System.Text.Json`既定値埋めの挙動）。本節はこれを踏まえ、作成・編集いずれの送信ハンドラでも`isDefaultForStage`キーを必ずペイロードへ含める（2.16.3節で確認）。
- `GET /api/templates`・`GET /api/templates/{id}`のレスポンスは`PromptTemplate`オブジェクトをそのまま返す実装であり（COMP-11、DTOによる投影なし）、`IsDefaultForStage`プロパティは既定のcamelCase命名規則により`isDefaultForStage`としてレスポンスJSONへ含まれる。`selectTemplate`（既存331〜336行目）はテンプレート選択のたびに`api(`/api/templates/${id}`)`で最新値を取得するため、`renderTemplateDetail`に渡される`t.isDefaultForStage`は常にサーバー側の最新値である。
- `api()`（既存10〜21行目）・`escapeAttr`（既存391〜393行目）・`STAGES`（既存1〜7行目）は本節で変更しない。

#### 2.16.1 `#template-form`（新規作成フォーム）へのチェックボックス追加とPOSTペイロードへの反映

`index.html`の`#template-form`（既存46〜61行目）内、本文`<textarea>`のラベルの直後・送信ボタンの直前にチェックボックスを追加する。

```html
<!-- 変更後（既存57〜60行目相当の直後に追加） -->
<label>本文（{{issue.title}} 等のプレースホルダ使用可）
  <textarea id="template-body" rows="6" required></textarea>
</label>
<label><input type="checkbox" id="template-is-default" /> この工程の既定テンプレートにする</label>
<button type="submit">作成</button>
```

**チェックボックスの初期状態**: 新規作成フォームであり反映すべきサーバー側の既存値が存在しないため、`checked`属性は付与しない（未チェックが既定）。他テンプレートの既定状態に影響を与えない安全側の初期値である。

`app.js`のPOST送信ハンドラ（既存373〜385行目）を以下のとおり変更する。

```js
document.getElementById("template-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  await api("/api/templates", {
    method: "POST",
    body: JSON.stringify({
      name: document.getElementById("template-name").value,
      stage: document.getElementById("template-stage").value,
      body: document.getElementById("template-body").value,
      isDefaultForStage: document.getElementById("template-is-default").checked,   // 新規（COMP-16、REQ-15）
    }),
  });
  e.target.reset();
  await loadTemplates();
});
```

`e.target.reset()`（既存）はネイティブの`<form>`リセット処理であり、チェックボックスの`checked`状態もHTML標準の挙動として未チェックへ戻る（本節で追加の対応は不要）。

#### 2.16.2 `renderTemplateDetail`（編集フォーム）へのチェックボックス追加とPUTペイロードへの反映

`renderTemplateDetail`（既存338〜371行目）が生成する`#template-edit-form`（342〜350行目）内、本文`<textarea>`のラベルの直後・送信/削除ボタンの直前にチェックボックスを追加する。

```js
function renderTemplateDetail(t) {
  const el = document.getElementById("template-detail");
  const stageOptions = STAGES.map((s) => `<option value="${s.value}" ${s.value === t.stage ? "selected" : ""}>${s.label}</option>`).join("");
  el.innerHTML = `
    <form id="template-edit-form">
      <label>工程<select id="te-stage">${stageOptions}</select></label>
      <label>名前<input type="text" id="te-name" value="${escapeAttr(t.name)}" required /></label>
      <label>本文<textarea id="te-body" rows="6" required>${escapeHtml(t.body)}</textarea></label>
      <label><input type="checkbox" id="te-is-default" ${t.isDefaultForStage ? "checked" : ""} /> この工程の既定テンプレートにする</label>
      <div style="display:flex; gap:0.5rem;">
        <button type="submit">保存</button>
        <button type="button" id="te-delete" class="danger">削除</button>
      </div>
    </form>
  `;
  document.getElementById("template-edit-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    await api(`/api/templates/${t.id}`, {
      method: "PUT",
      body: JSON.stringify({
        name: document.getElementById("te-name").value,
        stage: document.getElementById("te-stage").value,
        body: document.getElementById("te-body").value,
        isDefaultForStage: document.getElementById("te-is-default").checked,   // 新規（COMP-16、REQ-15）
      }),
    });
    await loadTemplates();
  });
  document.getElementById("te-delete").addEventListener("click", async () => {
    if (!confirm("削除しますか？")) return;
    await api(`/api/templates/${t.id}`, { method: "DELETE" });
    selectedTemplateId = null;
    document.getElementById("template-detail").innerHTML = "<p class='hint'>左の一覧から選択すると編集・削除できます。</p>";
    await loadTemplates();
  });
}
```

**`t.isDefaultForStage`が`undefined`の場合の扱い（境界値）**: `${t.isDefaultForStage ? "checked" : ""}`はJavaScriptの真偽評価であり、`t.isDefaultForStage`が`false`はもちろん`undefined`（例えば旧バージョンのデータで当該プロパティ自体が存在しない移行前データ等）であっても等しく`""`（未チェック）を返す。例外は発生しない。

**変更しない箇所**: `#te-delete`のclickハンドラ（既存364〜370行目）は`isDefaultForStage`を一切参照しないため変更しない。削除対象のテンプレートが既定テンプレートであった場合の扱い（既定が0件になった状態の是非）はcomponent_design.md COMP-16節に記載がなく、COMP-08（`ResolveDefaultTemplate`）側の既知の範囲外事項であり、本節では新たな挙動を追加しない。

#### 2.16.3 discovered issue確認: COMP-14の`loopEnabled`事例との比較（対応不要の結論）

COMP-14 2.14.1節は、チェックボックス等のUI要素を追加せず「クロージャに保持された現在値」（`issue.loopEnabled`）をそのままPUTペイロードへ含める設計を採ったため、フォーム表示中にサーバー側の値が変化していた場合に古い値を誤って送信してしまう限界（同節「この対応の限界」）を抱えていた。COMP-16でも同種の見落とし（チェックボックスの値をペイロードに含め忘れる、または古い値を送ってしまう）がないか確認した。

- **含め忘れの有無**: 2.16.1節・2.16.2節のとおり、POST・PUT双方の送信ハンドラで`isDefaultForStage`キーを明示的にペイロードへ含めている。含め忘れた場合は`SaveTemplateRequest.IsDefaultForStage`が既定値`false`として送信され、意図せず既定フラグが解除される、またはチェックを入れたつもりが反映されない不具合になるところであった（事前条件節参照）。
- **古い値を送ってしまう経路の有無**: COMP-14の`loopEnabled`はDOM上にUI要素を持たず`issue`オブジェクト（クロージャ変数、`selectIssue`実行時点のスナップショット）から値を読んでいたためスナップネッサの陳腐化が問題になった。COMP-16の`isDefaultForStage`は、作成・編集いずれのフォームでも実際の`<input type="checkbox">`要素（`#template-is-default`・`#te-is-default`）としてDOMへ存在し、送信ハンドラは`document.getElementById(...).checked`というDOM要素の現在値を都度読み取る。これは`#te-stage`・`#te-name`・`#te-body`という既存3項目と全く同じ「フォーム要素の現在値をそのまま読む」パターンであり、クロージャ変数を経由する特別な扱いを一切必要としない。したがって、COMP-14が抱えていた「表示中にサーバー側の値が変化した場合に取り残される」種類の陳腐化は、そもそも本節の設計では発生しない（チェックボックス自体が編集対象そのものであるため）。
- **結論**: COMP-14のような追加対応（クロージャ値の明示送信、限界の申し送り）は不要である。理由はcomponent_design.mdの確定方針どおりチェックボックスという実UI要素を素直に追加したことにより、COMP-14が直面した「UI要素を追加せずクロージャ値で代替する」という設計上の妥協自体が発生しないためである。

**編集フォームを開いたまま他テンプレートの既定状態が変化した場合の表示追随（申し送り、対応不要）**: あるテンプレートAの編集フォームを開いたまま、同一Stageの別テンプレートBを既定にする保存を行った場合、COMP-03 2.3.2節の一意性解決によりサーバー側ではAの`IsDefaultForStage`が`false`へ降格されるが、Aの編集フォーム（`#te-is-default`のチェック状態）はこの降格を購読しておらず、ユーザーが`selectTemplate(A.id)`を再実行（一覧からAを再選択）するまで画面上は古い状態（チェック済み）のまま残る。component_design.mdの確定方針（「一覧再取得時に他テンプレートのisDefaultForStageが自動的に更新されて見える」）は`selectTemplate`による再取得を前提としており、開きっぱなしの編集フォームへのリアルタイム反映までは要求していない。COMP-12/14いずれもポーリング機構を持たない本UI全般の設計上の限界（COMP-14 2.14.1節「この対応の限界」と同種）であり、COMP-16単体で解消すべき対象ではないため追加対応は行わない。

**代表的な境界値・分岐条件**:

| # | 状況 | 挙動 |
|---|---|---|
| 1 | 新規作成フォームで`#template-is-default`を未チェックのまま「作成」を送信 | `isDefaultForStage: false`が送信される。`ResolveDemotions`（COMP-03 2.3.2節）は`candidate.IsDefaultForStage === false`のため空リストを返し、既存の他テンプレートには一切影響しない |
| 2 | 新規作成フォームで`#template-is-default`をチェックして「作成」を送信、かつ同一Stageに既に既定テンプレートが1件存在する | `isDefaultForStage: true`が送信される。COMP-11ハンドラが`ResolveDemotions`の結果に従い既存の1件を`IsDefaultForStage=false`で降格保存した後、新規テンプレートを既定として保存する（COMP-11 2.11.6節境界値#2と同一の後勝ち方式） |
| 3 | 編集フォームを開いた時点で`t.isDefaultForStage === true`（サーバー側で既に既定） | `#te-is-default`に`checked`属性が付与された状態で表示される |
| 4 | 編集フォームを開いた時点で`t.isDefaultForStage === false`、または`undefined`（境界値、移行前データ等） | `#te-is-default`は未チェックで表示される（例外は発生しない） |
| 5 | 既定テンプレートの編集フォームで、既定フラグ以外の項目（名前・本文等）のみを変更し`#te-is-default`はチェックしたまま「保存」を送信 | `isDefaultForStage: true`が送信される。`ResolveDemotions`は`candidate.Id`（自分自身）を自己除外するため、他テンプレートへの副作用は発生しない（COMP-03 2.3.2節「`allTemplates`に`candidate`自身の更新前の値が含まれることについて」参照）。既定フラグが意図せず解除されることはない（COMP-14の`loopEnabled`同様のペイロード欠落は、2.16.3節の確認により本節では発生しない） |
| 6 | 既定テンプレートの編集フォームで、既定フラグのチェックを外して「保存」を送信 | `isDefaultForStage: false`が送信され、対象テンプレートの既定フラグが解除される。この結果、当該Stageに既定テンプレートが0件になり得るが、これはcomponent_design.md・COMP-03いずれも禁止していない状態遷移であり、本節で追加のガード（確認ダイアログ等）は設けない |
| 7 | テンプレートAの編集フォームを開いたまま、別タブ操作等で同一StageのテンプレートBが新たに既定にされた（サーバー側でAが降格済み） | Aの編集フォーム（`#te-is-default`）はこの変化を購読しておらず、チェック済みのまま表示され続ける。`selectTemplate(A.id)`が再実行されるまで解消しない（2.16.3節「編集フォームを開いたまま…」参照、対応不要の申し送り事項） |

対応ID: REQ-15
