import { buildSystemPrompt } from "./config.js";

export function parseLlmResponse(json) {
  if (!json) return "";
  if (json.response) return json.response;

  if (json.choices && Array.isArray(json.choices) && json.choices.length > 0) {
    const choice = json.choices[0];
    const message = choice?.message;
    if (message?.content) {
      if (typeof message.content === "string") {
        return message.content;
      }

      if (Array.isArray(message.content)) {
        return message.content
          .map((part) => {
            if (typeof part?.text === "string") return part.text;
            if (typeof part === "string") return part;
            return "";
          })
          .join("")
          .trim();
      }
    }
  }
  if (typeof json.content === "string") return json.content;
  return JSON.stringify(json);
}

export async function callProcessApi({ text, action, extraPrompt }, cfg) {
  const payload = {
    text,
    action,
    extraPrompt
  };

  const resp = await fetch(cfg.actionEndpoint, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  const data = await safeJson(resp);
  if (!resp.ok) {
    const message = data?.error || `Request failed with status ${resp.status}`;
    throw new Error(message);
  }
  return parseLlmResponse(data);
}

export async function callChatCompletion(messages, cfg, { signal } = {}) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), Math.max(5000, cfg.requestTimeoutMs || 45000));
  const mergedSignal = signal
    ? mergeSignals([signal, controller.signal])
    : controller.signal;

  const headers = { "Content-Type": "application/json", Accept: "application/json" };
  if (cfg.apiKey) {
    headers.Authorization = `Bearer ${cfg.apiKey}`;
  }

  const payload = {
    model: cfg.model,
    temperature: cfg.temperature,
    max_tokens: cfg.maxTokens,
    messages
  };

  try {
    const resp = await fetch(cfg.endpoint, {
      method: "POST",
      headers,
      body: JSON.stringify(payload),
      signal: mergedSignal
    });

    const data = await safeJson(resp);
    if (!resp.ok) {
      const message = data?.error?.message || data?.error || resp.statusText || "Request failed";
      throw new Error(message);
    }
    return parseLlmResponse(data);
  } finally {
    clearTimeout(timeout);
  }
}

export function buildShortcutMessages(shortcut, userInput, cfg, imageDataUrl) {
  const systemPrompt = buildSystemPrompt(cfg.language, shortcut.prompt);
  const content = [
    { type: "text", text: userInput }
  ];

  if (imageDataUrl) {
    content.push({
      type: "image_url",
      image_url: { url: imageDataUrl }
    });
  }

  return [
    {
      role: "system",
      content: [{ type: "text", text: systemPrompt }]
    },
    {
      role: "user",
      content
    }
  ];
}

function mergeSignals(signals) {
  const controller = new AbortController();
  const onAbort = () => controller.abort();
  for (const signal of signals) {
    if (!signal) continue;
    if (signal.aborted) {
      controller.abort();
      break;
    }
    signal.addEventListener("abort", onAbort, { once: true });
  }
  return controller.signal;
}

async function safeJson(resp) {
  try {
    return await resp.json();
  } catch {
    return null;
  }
}
