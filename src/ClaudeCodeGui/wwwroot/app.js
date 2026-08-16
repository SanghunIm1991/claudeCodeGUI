const STAGES = [
  { value: "requirements", label: "要件定義" },
  { value: "design", label: "設計" },
  { value: "implementation", label: "実装" },
  { value: "testing", label: "テスト" },
  { value: "deployment", label: "デプロイ" },
];
const stageLabel = (v) => STAGES.find((s) => s.value === v)?.label ?? v;

async function api(path, options) {
  const res = await fetch(path, {
    headers: { "Content-Type": "application/json" },
    ...options,
  });
  if (!res.ok) {
    const body = await res.text();
    throw new Error(`${res.status}: ${body}`);
  }
  if (res.status === 204) return null;
  return res.json();
}

// ---------------- Navigation ----------------
document.getElementById("nav-issues").addEventListener("click", () => switchView("issues"));
document.getElementById("nav-templates").addEventListener("click", () => switchView("templates"));

function switchView(name) {
  for (const view of ["issues", "templates"]) {
    document.getElementById(`view-${view}`).hidden = view !== name;
    document.getElementById(`nav-${view}`).classList.toggle("active", view === name);
  }
}

// ---------------- Issues ----------------
let issues = [];
let templates = [];
let selectedIssueId = null;
let selectedTemplateId = null;
let activeEventSource = null;
let currentArtifactDir = "";

async function loadIssues() {
  issues = await api("/api/issues");
  renderIssueList();
}

function renderIssueList() {
  const ul = document.getElementById("issue-list");
  ul.innerHTML = "";
  if (issues.length === 0) {
    ul.innerHTML = "<li class='hint'>Issueがありません</li>";
  }
  for (const issue of issues) {
    const li = document.createElement("li");
    li.className = issue.id === selectedIssueId ? "selected" : "";
    li.innerHTML = `${escapeHtml(issue.title)}<span class="meta">${stageLabel(issue.currentStage)} / ${issue.status}</span>`;
    li.addEventListener("click", () => selectIssue(issue.id));
    ul.appendChild(li);
  }
}

async function selectIssue(id) {
  selectedIssueId = id;
  renderIssueList();
  const issue = await api(`/api/issues/${id}`);
  const runs = await api(`/api/issues/${id}/runs`);
  renderIssueDetail(issue, runs);
}

function renderIssueDetail(issue, runs) {
  const el = document.getElementById("issue-detail");
  const stageOptions = STAGES.map((s) => `<option value="${s.value}" ${s.value === issue.currentStage ? "selected" : ""}>${s.label}</option>`).join("");
  const templateOptions = templates
    .map((t) => `<option value="${t.id}" ${t.stage === issue.currentStage ? "" : ""}>[${stageLabel(t.stage)}] ${escapeHtml(t.name)}</option>`)
    .join("");

  el.innerHTML = `
    <div class="detail-header">
      <h2>${escapeHtml(issue.title)}</h2>
      <span class="badge">${issue.status}</span>
    </div>
    <form id="issue-edit-form">
      <label>タイトル<input type="text" id="e-title" value="${escapeAttr(issue.title)}" required /></label>
      <label>説明<textarea id="e-description" rows="3">${escapeHtml(issue.description)}</textarea></label>
      <label>対象プロジェクトパス<input type="text" id="e-target-path" value="${escapeAttr(issue.targetProjectPath)}" required /></label>
      <label>現在の工程<select id="e-stage">${stageOptions}</select></label>
      <label>ステータス
        <select id="e-status">
          <option value="open" ${issue.status === "open" ? "selected" : ""}>open</option>
          <option value="in_progress" ${issue.status === "in_progress" ? "selected" : ""}>in_progress</option>
          <option value="done" ${issue.status === "done" ? "selected" : ""}>done</option>
        </select>
      </label>
      <button type="submit">保存</button>
    </form>

    <div class="run-panel">
      <h3>工程実行</h3>
      <div class="run-controls">
        <select id="run-template">${templateOptions}</select>
        <select id="run-permission">
          <option value="acceptEdits" selected>acceptEdits（編集は自動承認）</option>
          <option value="bypassPermissions">bypassPermissions（全許可・注意）</option>
          <option value="plan">plan（計画のみ）</option>
        </select>
        <button id="run-start">実行</button>
        <button id="run-cancel" class="secondary" disabled>中止</button>
      </div>
      <div id="run-log" class="log-view"></div>
      <div class="run-history">
        <h4>実行履歴</h4>
        <table>
          <thead><tr><th>工程</th><th>状態</th><th>開始</th><th>結果</th></tr></thead>
          <tbody id="run-history-body"></tbody>
        </table>
      </div>
    </div>

    <div class="artifact-panel">
      <h3>成果物（対象プロジェクト配下）</h3>
      <div id="artifact-path" class="hint"></div>
      <div class="artifact-browser">
        <div id="artifact-tree" class="artifact-tree"></div>
        <div class="artifact-editor">
          <textarea id="artifact-content" placeholder="ファイルを選択してください"></textarea>
          <button id="artifact-save" class="secondary" disabled>保存</button>
        </div>
      </div>
    </div>
  `;

  document.getElementById("issue-edit-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const updated = await api(`/api/issues/${issue.id}`, {
      method: "PUT",
      body: JSON.stringify({
        title: document.getElementById("e-title").value,
        description: document.getElementById("e-description").value,
        targetProjectPath: document.getElementById("e-target-path").value,
        currentStage: document.getElementById("e-stage").value,
        status: document.getElementById("e-status").value,
      }),
    });
    await loadIssues();
    selectIssue(updated.id);
  });

  document.getElementById("run-start").addEventListener("click", () => startRun(issue.id));
  document.getElementById("run-cancel").addEventListener("click", () => cancelRun());

  renderRunHistory(runs);

  currentArtifactDir = "";
  loadArtifactDir(issue.id, "");
  document.getElementById("artifact-save").addEventListener("click", () => saveArtifact(issue.id));
}

function renderRunHistory(runs) {
  const body = document.getElementById("run-history-body");
  if (!body) return;
  body.innerHTML = runs
    .map(
      (r) => `<tr>
        <td>${stageLabel(r.stage)}</td>
        <td><span class="badge ${r.status}">${r.status}</span></td>
        <td>${new Date(r.startedAt).toLocaleString()}</td>
        <td>${escapeHtml((r.resultSummary || "").slice(0, 60))}</td>
      </tr>`
    )
    .join("");
}

let currentRunId = null;

async function startRun(issueId) {
  const templateId = document.getElementById("run-template").value;
  const permissionMode = document.getElementById("run-permission").value;
  if (!templateId) {
    alert("テンプレートがありません。先にプロンプトテンプレートを作成してください。");
    return;
  }

  const logView = document.getElementById("run-log");
  logView.textContent = "";
  document.getElementById("run-start").disabled = true;
  document.getElementById("run-cancel").disabled = false;

  const run = await api(`/api/issues/${issueId}/runs`, {
    method: "POST",
    body: JSON.stringify({ templateId, permissionMode }),
  });
  currentRunId = run.id;

  if (activeEventSource) activeEventSource.close();
  const es = new EventSource(`/api/runs/${run.id}/stream`);
  activeEventSource = es;

  es.onmessage = (ev) => {
    appendLogLine(logView, ev.data);
    if (ev.data.includes('"type":"result"')) {
      es.close();
      finishRun(issueId);
    }
  };
  es.onerror = () => {
    es.close();
    finishRun(issueId);
  };
}

function appendLogLine(logView, rawLine) {
  let text = rawLine;
  try {
    const obj = JSON.parse(rawLine);
    if (obj.type === "assistant") {
      const parts = (obj.message?.content ?? []).map((c) => c.text).filter(Boolean);
      text = parts.length ? `[assistant] ${parts.join(" ")}` : rawLine;
    } else if (obj.type === "result") {
      text = `[result] is_error=${obj.is_error} ${obj.result ?? ""}`;
    } else if (obj.type === "system") {
      text = `[system] session=${obj.session_id}`;
    } else {
      text = `[${obj.type}]`;
    }
  } catch {
    // JSON以外の行（[stderr]等）はそのまま表示
  }
  logView.textContent += text + "\n";
  logView.scrollTop = logView.scrollHeight;
}

async function finishRun(issueId) {
  document.getElementById("run-start").disabled = false;
  document.getElementById("run-cancel").disabled = true;
  currentRunId = null;
  const runs = await api(`/api/issues/${issueId}/runs`);
  renderRunHistory(runs);
  await loadIssues();
}

async function cancelRun() {
  if (!currentRunId) return;
  await api(`/api/runs/${currentRunId}/cancel`, { method: "POST" });
}

// ---------------- Artifacts ----------------
async function loadArtifactDir(issueId, relDir) {
  currentArtifactDir = relDir;
  document.getElementById("artifact-path").textContent = `/${relDir}`;
  const entries = await api(`/api/issues/${issueId}/artifacts?path=${encodeURIComponent(relDir)}`);
  const tree = document.getElementById("artifact-tree");
  tree.innerHTML = "";

  if (relDir) {
    const up = document.createElement("div");
    up.textContent = "⬆ ..";
    up.addEventListener("click", () => {
      const parent = relDir.split("/").slice(0, -1).join("/");
      loadArtifactDir(issueId, parent);
    });
    tree.appendChild(up);
  }

  for (const entry of entries) {
    const div = document.createElement("div");
    div.textContent = entry.isDirectory ? `📁 ${entry.name}` : `📄 ${entry.name}`;
    div.addEventListener("click", () => {
      if (entry.isDirectory) loadArtifactDir(issueId, entry.relativePath);
      else loadArtifactFile(issueId, entry.relativePath);
    });
    tree.appendChild(div);
  }
}

let currentArtifactFile = null;

async function loadArtifactFile(issueId, relPath) {
  try {
    const data = await api(`/api/issues/${issueId}/artifacts/content?path=${encodeURIComponent(relPath)}`);
    currentArtifactFile = relPath;
    document.getElementById("artifact-content").value = data.content;
    document.getElementById("artifact-save").disabled = false;
  } catch (e) {
    alert(`読み込みに失敗しました（バイナリファイルの可能性があります）: ${e.message}`);
  }
}

async function saveArtifact(issueId) {
  if (!currentArtifactFile) return;
  await api(`/api/issues/${issueId}/artifacts/content?path=${encodeURIComponent(currentArtifactFile)}`, {
    method: "PUT",
    body: JSON.stringify({ content: document.getElementById("artifact-content").value }),
  });
  alert("保存しました。");
}

// ---------------- Issue create form ----------------
document.getElementById("issue-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const issue = await api("/api/issues", {
    method: "POST",
    body: JSON.stringify({
      title: document.getElementById("issue-title").value,
      description: document.getElementById("issue-description").value,
      targetProjectPath: document.getElementById("issue-target-path").value,
    }),
  });
  e.target.reset();
  await loadIssues();
  selectIssue(issue.id);
});

// ---------------- Templates ----------------
async function loadTemplates() {
  templates = await api("/api/templates");
  renderTemplateList();
}

function renderTemplateList() {
  const ul = document.getElementById("template-list");
  ul.innerHTML = "";
  for (const t of templates) {
    const li = document.createElement("li");
    li.className = t.id === selectedTemplateId ? "selected" : "";
    li.innerHTML = `${escapeHtml(t.name)}<span class="meta">${stageLabel(t.stage)}</span>`;
    li.addEventListener("click", () => selectTemplate(t.id));
    ul.appendChild(li);
  }
}

async function selectTemplate(id) {
  selectedTemplateId = id;
  renderTemplateList();
  const t = await api(`/api/templates/${id}`);
  renderTemplateDetail(t);
}

function renderTemplateDetail(t) {
  const el = document.getElementById("template-detail");
  const stageOptions = STAGES.map((s) => `<option value="${s.value}" ${s.value === t.stage ? "selected" : ""}>${s.label}</option>`).join("");
  el.innerHTML = `
    <form id="template-edit-form">
      <label>工程<select id="te-stage">${stageOptions}</select></label>
      <label>名前<input type="text" id="te-name" value="${escapeAttr(t.name)}" required /></label>
      <label>本文<textarea id="te-body" rows="6" required>${escapeHtml(t.body)}</textarea></label>
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

document.getElementById("template-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  await api("/api/templates", {
    method: "POST",
    body: JSON.stringify({
      name: document.getElementById("template-name").value,
      stage: document.getElementById("template-stage").value,
      body: document.getElementById("template-body").value,
    }),
  });
  e.target.reset();
  await loadTemplates();
});

// ---------------- Utilities ----------------
function escapeHtml(str) {
  return String(str ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
function escapeAttr(str) {
  return escapeHtml(str);
}

// ---------------- Init ----------------
(async function init() {
  await loadTemplates();
  await loadIssues();
})();
