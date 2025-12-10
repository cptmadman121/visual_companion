export const STORAGE_KEY = "deskllmConfig";
export const CHAT_HISTORY_KEY = "deskllmChatHistory";

export const DEFAULT_PROMPTS = [
  {
    id: "capture-screen",
    name: "Capture Screen",
    hotkey: "Alt+Shift+S",
    prompt: "Describe the selected region succinctly.",
    activation: "CaptureScreen",
    showResponseDialog: true,
    action: "capture"
  },
  {
    id: "proofread",
    name: "Proofread Selection",
    hotkey: "Alt+Shift+P",
    prompt: "Proofread and improve grammar, spelling, and clarity, while maintaining the original language of the text. Preserve tone and meaning. Keep formatting, newlines, tabs etc. exactly as in the original text. Return only the corrected text.",
    activation: "ForegroundSelection",
    showResponseDialog: false,
    action: "proofread"
  },
  {
    id: "translate",
    name: "Translate Selection",
    hotkey: "Alt+Shift+T",
    prompt: "If the provided text is not in German, translate it into German. If the provided text is in German, translate it into English. The entire translation process should preserve the tone, structure, and formatting of the original text. Return only the translated text.",
    activation: "ForegroundSelection",
    showResponseDialog: false,
    action: "translate"
  },
  {
    id: "anonymize",
    name: "Anonymize Selection",
    hotkey: "Alt+Shift+A",
    prompt: "Anonymize the provided text. Replace personal data such as real names, email addresses, phone numbers, or postal addresses with fictitious placeholders from the shows 'The Simpsons' or 'Futurama'. Preserve formatting and return only the sanitized text.",
    activation: "ForegroundSelection",
    showResponseDialog: false
  }
];

export const DEFAULT_CONFIG = {
  endpoint: "https://ollama.test.cipsoft.de/v1/chat/completions",
  actionEndpoint: "http://127.0.0.1:27124/v1/process",
  model: "gemma3:27b",
  temperature: 0.2,
  maxTokens: 32000,
  requestTimeoutMs: 45000,
  language: "English",
  useVision: true,
  preferLocalApi: true,
  apiKey: "",
  promptShortcuts: DEFAULT_PROMPTS
};

const promptDefaultsById = Object.fromEntries(DEFAULT_PROMPTS.map((p) => [p.id, p]));

export function normalizeShortcut(raw, index = 0) {
  const fallback = promptDefaultsById[raw?.id] ?? DEFAULT_PROMPTS[index] ?? {};
  const shortcut = {
    ...fallback,
    ...(raw || {})
  };

  if (!shortcut.id) {
    shortcut.id = crypto.randomUUID?.() ?? `shortcut-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }
  shortcut.name = shortcut.name || fallback.name || `Shortcut ${index + 1}`;
  shortcut.hotkey = shortcut.hotkey || fallback.hotkey || "";
  shortcut.prompt = shortcut.prompt || fallback.prompt || "";
  shortcut.activation = shortcut.activation || fallback.activation || "ForegroundSelection";
  shortcut.showResponseDialog = shortcut.showResponseDialog ?? fallback.showResponseDialog ?? false;
  return shortcut;
}

export function normalizeConfig(rawConfig) {
  const cfg = {
    ...DEFAULT_CONFIG,
    ...(rawConfig || {})
  };

  const shortcuts = Array.isArray(rawConfig?.promptShortcuts) && rawConfig.promptShortcuts.length > 0
    ? rawConfig.promptShortcuts
    : DEFAULT_PROMPTS;

  cfg.promptShortcuts = shortcuts.map((p, idx) => normalizeShortcut(p, idx));
  cfg.temperature = safeNumber(cfg.temperature, DEFAULT_CONFIG.temperature);
  cfg.maxTokens = safeNumber(cfg.maxTokens, DEFAULT_CONFIG.maxTokens);
  cfg.requestTimeoutMs = safeNumber(cfg.requestTimeoutMs, DEFAULT_CONFIG.requestTimeoutMs);
  cfg.useVision = Boolean(cfg.useVision);
  cfg.preferLocalApi = cfg.preferLocalApi !== false;
  return cfg;
}

function safeNumber(value, fallback) {
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

export async function loadConfig() {
  const stored = await chrome.storage.sync.get(STORAGE_KEY);
  return normalizeConfig(stored?.[STORAGE_KEY]);
}

export async function saveConfig(config) {
  const normalized = normalizeConfig(config);
  await chrome.storage.sync.set({ [STORAGE_KEY]: normalized });
  return normalized;
}

export async function loadChatHistory() {
  const stored = await chrome.storage.local.get(CHAT_HISTORY_KEY);
  return stored?.[CHAT_HISTORY_KEY] ?? [];
}

export async function saveChatHistory(history) {
  await chrome.storage.local.set({ [CHAT_HISTORY_KEY]: history });
}

export function buildSystemPrompt(language, extra) {
  const basePrompt =
    (language || "").toLowerCase() === "german"
      ? "You are TrayVisionPrompt, a helpful assistant that responds in fluent German unless explicitly instructed otherwise."
      : "You are TrayVisionPrompt, a helpful assistant that responds in fluent English unless explicitly instructed otherwise.";

  if (!extra || !String(extra).trim()) {
    return basePrompt;
  }

  return `${basePrompt}\n\n${extra}`.trim();
}
