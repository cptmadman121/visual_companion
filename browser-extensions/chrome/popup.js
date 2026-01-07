import {
  DEFAULT_PROMPTS,
  buildSystemPrompt,
  loadChatHistory,
  loadConfig,
  normalizeConfig,
  saveChatHistory,
  saveConfig
} from "./shared/config.js";
import { callChatCompletion } from "./shared/llm.js";

let state = {
  cfg: null,
  chatHistory: [],
  mode: "popup",
  sending: false
};

document.addEventListener("DOMContentLoaded", async () => {
  state.mode = document.body.dataset.mode || "popup";
  state.cfg = await loadConfig();
  state.chatHistory = await loadChatHistory();
  renderShell();
  renderShortcuts();
  renderHistory();
  fillSettingsForm();
  wireInteractions();
});

function renderShell() {
  const app = document.getElementById("app");
  app.innerHTML = `
    <header class="top">
      <div>
        <div class="title">deskLLM</div>
        <div class="subtitle">Hotkeys, context menu, and chat inside Chrome.</div>
      </div>
      <div class="status" id="status">Ready</div>
    </header>
    <div class="tabs">
      <button class="tab-btn active" data-tab="chat">Chat</button>
      <button class="tab-btn" data-tab="shortcuts">Shortcuts</button>
      <button class="tab-btn" data-tab="settings">Connection</button>
    </div>
    <section class="tab-content active" data-tab-content="chat">
      <div class="chat-log" id="chat-log"></div>
      <div class="composer">
        <textarea id="chat-input" placeholder="Ask deskLLM anything..."></textarea>
        <div class="composer-actions">
          <button id="reset-chat" class="ghost">Reset</button>
          <button id="send-chat" class="primary">Send</button>
        </div>
      </div>
    </section>
    <section class="tab-content" data-tab-content="shortcuts">
      <div class="section-head">
        <div>
          <h3>Prompt shortcuts</h3>
          <p>These drive the context menu entries and hotkeys (edit in chrome://extensions/shortcuts).</p>
        </div>
        <div class="pill-actions">
          <button id="save-shortcuts" class="ghost">Save</button>
          <button id="add-shortcut" class="ghost">Add prompt</button>
        </div>
      </div>
      <div id="shortcuts"></div>
      <div class="hint">Tray menu equivalents live under the “deskLLM” entry in your Chrome context menu.</div>
    </section>
    <section class="tab-content" data-tab-content="settings">
      <h3>Connection</h3>
      <div class="grid">
        <label>Chat endpoint
          <input id="endpoint" type="text" placeholder="https://host/v1/chat/completions" />
        </label>
        <label>Action endpoint (local API)
          <input id="action-endpoint" type="text" placeholder="http://127.0.0.1:27124/v1/process" />
        </label>
        <label>Model
          <input id="model" type="text" />
        </label>
        <label>API key (optional)
          <input id="api-key" type="password" autocomplete="off" />
        </label>
        <label>Language
          <select id="language">
            <option value="English">English</option>
            <option value="German">German</option>
          </select>
        </label>
        <label>Temperature
          <input id="temperature" type="number" step="0.1" min="0" max="2" />
        </label>
        <label>Max tokens
          <input id="max-tokens" type="number" min="512" max="64000" />
        </label>
        <label>Request timeout (ms)
          <input id="request-timeout" type="number" min="5000" max="180000" />
        </label>
        <label class="checkbox">
          <input id="prefer-local" type="checkbox" />
          <span>Prefer local /v1/process for proofread & translate</span>
        </label>
        <label class="checkbox">
          <input id="use-vision" type="checkbox" />
          <span>Allow vision for capture shortcuts</span>
        </label>
      </div>
      <div class="actions">
        <button id="test-connection" class="ghost">Test connection</button>
        <button id="save-config" class="primary">Save</button>
      </div>
      <div class="hint">
        Hotkeys: open chrome://extensions/shortcuts and bind the deskLLM commands (Proofread, Translate, Anonymize, Capture, Open panel).
      </div>
    </section>
  `;
}

function wireInteractions() {
  document.querySelectorAll(".tab-btn").forEach((btn) => {
    btn.addEventListener("click", () => switchTab(btn.dataset.tab));
  });
  document.getElementById("send-chat").addEventListener("click", sendChatMessage);
  document.getElementById("reset-chat").addEventListener("click", resetChat);
  document.getElementById("save-config").addEventListener("click", saveConfiguration);
  document.getElementById("test-connection").addEventListener("click", testConnection);
  document.getElementById("add-shortcut").addEventListener("click", addShortcut);
  document.getElementById("save-shortcuts").addEventListener("click", saveShortcutsOnly);
}

function switchTab(tab) {
  document.querySelectorAll(".tab-btn").forEach((btn) => btn.classList.toggle("active", btn.dataset.tab === tab));
  document.querySelectorAll(".tab-content").forEach((section) =>
    section.classList.toggle("active", section.dataset.tabContent === tab)
  );
}

function renderHistory() {
  const log = document.getElementById("chat-log");
  log.innerHTML = "";
  state.chatHistory.forEach((entry) => {
    const row = document.createElement("div");
    row.className = `message ${entry.role}`;
    const author = document.createElement("div");
    author.className = "author";
    author.textContent = entry.role === "user" ? "You" : "deskLLM";
    const body = document.createElement("div");
    body.className = "body";
    body.textContent = entry.content;
    row.append(author, body);
    log.appendChild(row);
  });
  log.scrollTop = log.scrollHeight;
}

function renderShortcuts() {
  const container = document.getElementById("shortcuts");
  container.innerHTML = "";
  state.cfg.promptShortcuts.forEach((shortcut) => {
    container.appendChild(renderShortcutCard(shortcut));
  });
}

function renderShortcutCard(shortcut) {
  const card = document.createElement("div");
  card.className = "shortcut-card";
  card.dataset.id = shortcut.id;
  card.innerHTML = `
    <div class="row">
      <label>Name <input data-field="name" type="text" value="${escapeHtml(shortcut.name)}" /></label>
      <label>Hotkey label <input data-field="hotkey" type="text" value="${escapeHtml(shortcut.hotkey || "")}" /></label>
      <label>Action <input data-field="action" type="text" value="${escapeHtml(shortcut.action || "")}" placeholder="proofread | translate | leave empty" /></label>
      <label>Activation
        <select data-field="activation">
          ${["ForegroundSelection", "TextDialog", "CaptureScreen", "CaptureScreenFast"]
            .map((opt) => `<option value="${opt}" ${shortcut.activation === opt ? "selected" : ""}>${opt}</option>`)
            .join("")}
        </select>
      </label>
    </div>
    <label>Prompt
      <textarea data-field="prompt" rows="3">${escapeHtml(shortcut.prompt || "")}</textarea>
    </label>
    <label>Prefill (dialog only)
      <input data-field="prefill" type="text" value="${escapeHtml(shortcut.prefill || "")}" />
    </label>
    <label class="checkbox">
      <input data-field="dialog" type="checkbox" ${shortcut.showResponseDialog ? "checked" : ""} />
      <span>Always show response dialog</span>
    </label>
    <div class="card-actions">
      <button class="ghost danger" data-action="remove">Remove</button>
    </div>
  `;

  card.querySelector("[data-action='remove']").addEventListener("click", () => {
    state.cfg.promptShortcuts = state.cfg.promptShortcuts.filter((p) => p.id !== shortcut.id);
    renderShortcuts();
  });

  return card;
}

async function sendChatMessage() {
  if (state.sending) return;
  const input = document.getElementById("chat-input");
  const content = input.value.trim();
  if (!content) return;

  state.sending = true;
  setStatus("Sending...", "busy");
  state.chatHistory.push({ role: "user", content });
  renderHistory();
  input.value = "";

  try {
    const messages = buildChatMessages(state.chatHistory, state.cfg);
    const response = await callChatCompletion(messages, state.cfg);
    state.chatHistory.push({ role: "assistant", content: response });
    await saveChatHistory(state.chatHistory);
    renderHistory();
    setStatus("Ready", "ok");
  } catch (err) {
    setStatus(`Error: ${err.message || err}`);
  } finally {
    state.sending = false;
  }
}

function buildChatMessages(history, cfg) {
  const system = buildSystemPrompt(cfg.language);
  const messages = [
    { role: "system", content: system }
  ];
  history.forEach((entry) => {
    messages.push({ role: entry.role, content: entry.content });
  });
  return messages;
}

async function resetChat() {
  state.chatHistory = [];
  await saveChatHistory(state.chatHistory);
  renderHistory();
}

async function saveConfiguration() {
  const cfg = { ...state.cfg };
  cfg.endpoint = document.getElementById("endpoint").value.trim() || cfg.endpoint;
  cfg.actionEndpoint = document.getElementById("action-endpoint").value.trim() || cfg.actionEndpoint;
  cfg.model = document.getElementById("model").value.trim() || cfg.model;
  cfg.apiKey = document.getElementById("api-key").value;
  cfg.language = document.getElementById("language").value;
  cfg.temperature = Number(document.getElementById("temperature").value) || cfg.temperature;
  cfg.maxTokens = Number(document.getElementById("max-tokens").value) || cfg.maxTokens;
  cfg.requestTimeoutMs = Number(document.getElementById("request-timeout").value) || cfg.requestTimeoutMs;
  cfg.preferLocalApi = document.getElementById("prefer-local").checked;
  cfg.useVision = document.getElementById("use-vision").checked;
  cfg.promptShortcuts = readShortcutCards();

  state.cfg = normalizeConfig(cfg);
  await saveConfig(state.cfg);
  await chrome.runtime.sendMessage({ type: "deskllm/reload-context" });
  setStatus("Saved.", "ok");
  renderShortcuts();
}

async function saveShortcutsOnly() {
  state.cfg.promptShortcuts = readShortcutCards();
  state.cfg = normalizeConfig(state.cfg);
  await saveConfig(state.cfg);
  await chrome.runtime.sendMessage({ type: "deskllm/reload-context" });
  setStatus("Shortcuts saved.", "ok");
  renderShortcuts();
}

function readShortcutCards() {
  const cards = Array.from(document.querySelectorAll(".shortcut-card"));
  if (cards.length === 0) return DEFAULT_PROMPTS;

  return cards.map((card) => ({
    id: card.dataset.id || crypto.randomUUID?.() || `shortcut-${Date.now()}`,
    name: valueOf(card, "name"),
    hotkey: valueOf(card, "hotkey"),
    prompt: valueOf(card, "prompt"),
    prefill: valueOf(card, "prefill"),
    activation: valueOf(card, "activation"),
    showResponseDialog: card.querySelector("[data-field='dialog']").checked,
    action: valueOf(card, "action")
  }));
}

function valueOf(card, field) {
  const el = card.querySelector(`[data-field='${field}']`);
  if (!el) return "";
  return el.value?.trim?.() ?? "";
}

function fillSettingsForm() {
  const cfg = state.cfg;
  document.getElementById("endpoint").value = cfg.endpoint || "";
  document.getElementById("action-endpoint").value = cfg.actionEndpoint || "";
  document.getElementById("model").value = cfg.model || "";
  document.getElementById("api-key").value = cfg.apiKey || "";
  document.getElementById("language").value = cfg.language || "English";
  document.getElementById("temperature").value = cfg.temperature;
  document.getElementById("max-tokens").value = cfg.maxTokens;
  document.getElementById("request-timeout").value = cfg.requestTimeoutMs;
  document.getElementById("prefer-local").checked = cfg.preferLocalApi;
  document.getElementById("use-vision").checked = cfg.useVision;
}

async function testConnection() {
  const cfg = normalizeConfig({
    ...state.cfg,
    endpoint: document.getElementById("endpoint").value.trim() || state.cfg.endpoint,
    model: document.getElementById("model").value.trim() || state.cfg.model,
    apiKey: document.getElementById("api-key").value
  });
  setStatus("Testing...", "pending");
  try {
    const messages = [
      { role: "system", content: buildSystemPrompt(cfg.language) },
      { role: "user", content: "Reply with a single word: ready." }
    ];
    const response = await callChatCompletion(messages, cfg);
    setStatus(`Connection OK: ${response.substring(0, 60)}`, "ok");
  } catch (err) {
    setStatus(`Test failed: ${err.message || err}`, "error");
  }
}

function addShortcut() {
  const newShortcut = {
    id: crypto.randomUUID?.() || `shortcut-${Date.now()}`,
    name: "New prompt",
    hotkey: "Alt+Shift+Y",
    prompt: "",
    prefill: "",
    activation: "ForegroundSelection",
    showResponseDialog: false
  };
  state.cfg.promptShortcuts.push(newShortcut);
  renderShortcuts();
}

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function setStatus(text, level = "info") {
  const el = document.getElementById("status");
  if (el) {
    el.textContent = text;
    el.classList.remove("ok", "error", "info", "pending", "busy", "status-flash");
    el.classList.add(level);
    if (level === "ok") {
      // retrigger animation
      void el.offsetWidth;
      el.classList.add("status-flash");
    }
  }
}
