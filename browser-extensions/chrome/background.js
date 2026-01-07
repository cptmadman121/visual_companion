import { DEFAULT_CONFIG, STORAGE_KEY, loadConfig, normalizeConfig, saveConfig } from "./shared/config.js";
import { buildShortcutMessages, callChatCompletion, callProcessApi } from "./shared/llm.js";

const MENU_ROOT = "deskllm/root";
const MENU_OPEN_UI = "deskllm/open-ui";
const MENU_OPEN_OPTIONS = "deskllm/open-options";
const supportsIconAnimation = typeof OffscreenCanvas !== "undefined" && typeof createImageBitmap === "function" && typeof chrome?.action?.setIcon === "function";
const ICON_PATHS = { "16": "icon.png", "24": "icon.png", "32": "icon.png", "48": "icon.png", "128": "icon.png" };

const COMMAND_TO_PROMPT = {
  proofread: "proofread",
  translate: "translate",
  anonymize: "anonymize",
  "open-panel": null
};

bootstrap();

let menuQueue = Promise.resolve();
let iconState = "idle";
let iconAnimInterval = null;
let iconProgress = 0;
let baseIconBitmap32 = null;
let baseIconBitmap24 = null;
let baseIconBitmap16 = null;
let useBadgeFallback = !supportsIconAnimation;

function queueMenuRegistration(cfg) {
  menuQueue = menuQueue
    .catch(() => {})
    .then(() => registerContextMenus(cfg));
  return menuQueue;
}

async function bootstrap() {
  const cfg = await ensureConfig();
  await queueMenuRegistration(cfg);
}

chrome.runtime.onInstalled.addListener(async () => {
  const cfg = await ensureConfig();
  await queueMenuRegistration(cfg);
});

chrome.storage.onChanged.addListener(async (changes, area) => {
  if (area === "sync" && changes[STORAGE_KEY]) {
    const cfg = normalizeConfig(changes[STORAGE_KEY].newValue);
    await queueMenuRegistration(cfg);
  }
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (!tab?.id) return;
  if (info.menuItemId === MENU_OPEN_UI || info.menuItemId === MENU_OPEN_OPTIONS) {
    await openOptionsPage();
    return;
  }

  if (typeof info.menuItemId === "string" && info.menuItemId.startsWith("deskllm/shortcut/")) {
    const shortcutId = info.menuItemId.replace("deskllm/shortcut/", "");
    const cfg = await loadConfig();
    const shortcut = cfg.promptShortcuts.find((p) => p.id === shortcutId);
    if (shortcut) {
      await handleShortcut(shortcut, tab, info.selectionText, true, info.frameId);
    }
  }
});

chrome.commands.onCommand.addListener(async (command) => {
  if (command === "open-panel") {
    await openOptionsPage();
    return;
  }

  const cfg = await loadConfig();
  const shortcutId = COMMAND_TO_PROMPT[command];
  if (!shortcutId) return;

  const shortcut = cfg.promptShortcuts.find((p) => p.id === shortcutId) ?? cfg.promptShortcuts.find((p) => p.name.toLowerCase().includes(shortcutId));
  if (!shortcut) return;

  const tab = await getActiveTab();
  if (tab?.id) {
    await handleShortcut(shortcut, tab, undefined, true, undefined);
  }
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "deskllm/reload-context") {
    loadConfig().then(queueMenuRegistration);
  }
  return false;
});

async function ensureConfig() {
  const cfg = await loadConfig();
  if (!cfg || Object.keys(cfg).length === 0) {
    await saveConfig(DEFAULT_CONFIG);
    return DEFAULT_CONFIG;
  }
  const normalized = normalizeConfig(cfg);
  await saveConfig(normalized);
  return normalized;
}

async function registerContextMenus(cfg) {
  await chrome.contextMenus.removeAll();
  await chrome.contextMenus.create({ id: MENU_ROOT, title: "deskLLM", contexts: ["all"] });
  await chrome.contextMenus.create({ id: MENU_OPEN_UI, parentId: MENU_ROOT, title: "Open deskLLM panel", contexts: ["all"] });
  await chrome.contextMenus.create({ id: MENU_OPEN_OPTIONS, parentId: MENU_ROOT, title: "Configuration", contexts: ["all"] });

  for (const shortcut of cfg.promptShortcuts) {
    await chrome.contextMenus.create({
      id: `deskllm/shortcut/${shortcut.id}`,
      parentId: MENU_ROOT,
      title: shortcut.name,
      contexts: shortcut.activation === "ForegroundSelection" ? ["selection"] : ["all"]
    });
  }
}

async function withWaitingBadge(fn) {
  try {
    await setIconStatus("busy");
    return await fn();
  } finally {
    await setIconStatus("idle");
  }
}

async function handleShortcut(shortcut, tab, contextSelectionText, fromHotkey = false, frameId) {
  if (!tab?.id) return;
  const cfg = await loadConfig();

  try {
    await setIconStatus("pending");
    if (shortcut.activation === "CaptureScreen" || shortcut.activation === "CaptureScreenFast") {
      const response = fromHotkey
        ? await withWaitingBadge(async () => handleCaptureShortcut(shortcut, cfg, tab))
        : await handleCaptureShortcut(shortcut, cfg, tab);
      if (response) {
        await showResponse(tab.id, shortcut, response, false);
      }
      return;
    }

    let userText = "";
    if (shortcut.activation === "TextDialog") {
      userText = await requestTextInput(tab.id, shortcut.prompt, shortcut.prefill, frameId);
    } else if (contextSelectionText && contextSelectionText.trim()) {
      userText = contextSelectionText;
    } else {
      userText = await requestSelection(tab.id, frameId);
    }

    if (!userText || !userText.trim()) {
      await showToast(tab.id, "deskLLM: No text selected.");
      return;
    }

    const response = fromHotkey
      ? await withWaitingBadge(async () => runShortcutPrompt(shortcut, userText.trim(), cfg))
      : await runShortcutPrompt(shortcut, userText.trim(), cfg);
    if (!response) {
      await showToast(tab.id, "deskLLM: No response received.");
      return;
    }

    if (shortcut.showResponseDialog) {
      await showResponse(tab.id, shortcut, response, shortcut.activation === "ForegroundSelection", frameId);
    } else {
      const replaced = await replaceSelection(tab.id, response, frameId);
      if (!replaced) {
        await showResponse(tab.id, shortcut, response, true, frameId);
      } else {
        await showToast(tab.id, `${shortcut.name} applied.`);
      }
    }
  } catch (err) {
    console.error("deskLLM shortcut failed", err);
    await showToast(tab.id, `deskLLM error: ${err.message || err}`);
  } finally {
    await setIconStatus("idle");
  }
}

async function handleCaptureShortcut(shortcut, cfg, tab) {
  try {
    const imageDataUrl = await captureCurrentTab(tab);
    if (!imageDataUrl) throw new Error("Could not capture the current tab.");
    const messages = buildShortcutMessages(shortcut, shortcut.prompt, cfg, imageDataUrl);
    return await callChatCompletion(messages, cfg);
  } catch (err) {
    await showToast(tab.id, `deskLLM capture failed: ${err.message || err}`);
    return null;
  }
}

async function runShortcutPrompt(shortcut, userText, cfg) {
  const usesProcessApi = cfg.preferLocalApi && cfg.actionEndpoint;
  if (usesProcessApi) {
    const apiAction = shortcut.action && shortcut.action !== "capture" ? shortcut.action : undefined;
    try {
      return await callProcessApi(
        {
          text: userText,
          action: apiAction,
          extraPrompt: shortcut.prompt
        },
        cfg
      );
    } catch (err) {
      console.warn("Local API failed, falling back to chat completion", err);
    }
  }

  const messages = buildShortcutMessages(shortcut, userText, cfg);
  return callChatCompletion(messages, cfg);
}

async function openOptionsPage() {
  if (chrome.runtime.openOptionsPage) {
    await chrome.runtime.openOptionsPage();
  } else {
    await chrome.tabs.create({ url: chrome.runtime.getURL("options.html") });
  }
}

async function getActiveTab() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  return tab;
}

async function requestSelection(tabId, frameId) {
  try {
    const [{ result }] = await chrome.scripting.executeScript({
      target: frameId !== undefined ? { tabId, frameIds: [frameId] } : { tabId },
      func: () => ({
        text:
          window.getSelection()?.toString() ||
          (document.activeElement && (document.activeElement.value || document.activeElement.innerText) || "")
      })
    });
    if (result?.text) return result.text;
  } catch (err) {
    console.warn("Failed to read selection via scripting API", err);
  }

  try {
    const resp = await chrome.tabs.sendMessage(tabId, { type: "deskllm/get-selection" }, frameId !== undefined ? { frameId } : undefined);
    return resp?.text || "";
  } catch {
    return "";
  }
}

async function requestTextInput(tabId, prompt, prefill, frameId) {
  try {
    const resp = await chrome.tabs.sendMessage(tabId, {
      type: "deskllm/request-text",
      prompt,
      prefill
    }, frameId !== undefined ? { frameId } : undefined);
    return resp?.text || "";
  } catch {
    return prefill || "";
  }
}

async function replaceSelection(tabId, text, frameId) {
  try {
    const resp = await chrome.tabs.sendMessage(tabId, { type: "deskllm/replace-selection", text }, frameId !== undefined ? { frameId } : undefined);
    return Boolean(resp?.ok);
  } catch (err) {
    console.warn("Replace selection failed", err);
    return false;
  }
}

async function showResponse(tabId, shortcut, text, allowReplace, frameId) {
  try {
    await chrome.tabs.sendMessage(tabId, {
      type: "deskllm/show-response",
      title: shortcut?.name || "deskLLM",
      text,
      allowReplace
    }, frameId !== undefined ? { frameId } : undefined);
  } catch (err) {
    console.warn("Failed to show response dialog", err);
  }
}

async function showToast(tabId, message) {
  try {
    await chrome.tabs.sendMessage(tabId, { type: "deskllm/show-toast", message });
  } catch {
    // ignore
  }
}

async function captureCurrentTab(tab) {
  const targetWindow = tab?.windowId ?? chrome.windows.WINDOW_ID_CURRENT;
  return chrome.tabs.captureVisibleTab(targetWindow, { format: "png" });
}

async function setIconStatus(state) {
  try {
    iconState = state;
    const badgeColor = state === "pending" ? "#f1c40f" : "#2ecc71";
    const animationEnabled = supportsIconAnimation && !useBadgeFallback;
    const fallback = !animationEnabled;

    if (fallback) {
      stopIconAnimation();
      if (state === "idle") {
        await setBadge("");
        return;
      }
      await setBadge(state === "pending" ? "…" : "·", badgeColor);
      return;
    }

    if (state === "idle") {
      stopIconAnimation();
      await setIconSafely({ path: ICON_PATHS }, badgeColor);
      await setBadge("");
      return;
    }

    await ensureBaseIconBitmap();
    startIconAnimation(badgeColor);
    await setBadge("");
  } catch (err) {
    console.warn("deskLLM setIconStatus failed; falling back to badge", err);
    useBadgeFallback = true;
    stopIconAnimation();
    await setBadge(state === "idle" ? "" : "·", state === "pending" ? "#f1c40f" : "#2ecc71");
    await setIconSafely({ path: ICON_PATHS }, state === "pending" ? "#f1c40f" : "#2ecc71");
  }
}

async function ensureBaseIconBitmap() {
  if (baseIconBitmap32 && baseIconBitmap24 && baseIconBitmap16) return;
  if (!supportsIconAnimation) {
    useBadgeFallback = true;
    return;
  }
  try {
    const res = await fetch(chrome.runtime.getURL("icon.png"));
    const blob = await res.blob();
    baseIconBitmap32 = await createImageBitmap(blob, { resizeWidth: 32, resizeHeight: 32, resizeQuality: "high" });
    baseIconBitmap24 = await createImageBitmap(blob, { resizeWidth: 24, resizeHeight: 24, resizeQuality: "high" });
    baseIconBitmap16 = await createImageBitmap(blob, { resizeWidth: 16, resizeHeight: 16, resizeQuality: "high" });
  } catch (err) {
    console.warn("deskLLM icon load failed; using badge fallback", err);
    useBadgeFallback = true;
  }
}

function startIconAnimation(color) {
  if (!supportsIconAnimation || useBadgeFallback) {
    setBadge("·", color).catch(() => {});
    return;
  }
  stopIconAnimation();
  iconProgress = 0;
  const tick = async () => {
    try {
      if (!baseIconBitmap32 || !baseIconBitmap24 || !baseIconBitmap16) {
        useBadgeFallback = true;
        await setBadge("·", color);
        return;
      }

      const imageData32 = await createEdgeIcon(baseIconBitmap32, color, iconProgress, 32);
      const imageData24 = await createEdgeIcon(baseIconBitmap24, color, iconProgress, 24);
      const imageData16 = await createEdgeIcon(baseIconBitmap16, color, iconProgress, 16);
      if (
        imageData16?.width === 16 &&
        imageData16?.height === 16 &&
        imageData24?.width === 24 &&
        imageData24?.height === 24 &&
        imageData32?.width === 32 &&
        imageData32?.height === 32
      ) {
        await setIconSafely({ imageData: { "16": imageData16, "24": imageData24, "32": imageData32 } }, color);
      } else {
        throw new Error("Icon dimensions invalid or missing");
      }
      iconProgress = (iconProgress + 0.12) % 1;
    } catch (err) {
      console.warn("deskLLM icon animation failed", err);
      stopIconAnimation();
      iconState = "idle";
      useBadgeFallback = true;
      await setBadge("·", color);
      await setIconSafely({ path: ICON_PATHS }, color);
    }
  };
  tick();
  iconAnimInterval = setInterval(tick, 120);
}

function stopIconAnimation() {
  if (iconAnimInterval) {
    clearInterval(iconAnimInterval);
    iconAnimInterval = null;
  }
}

async function createEdgeIcon(baseBitmap, color, progress, size) {
  if (typeof OffscreenCanvas === "undefined") return null;
  const canvas = new OffscreenCanvas(size, size);
  const ctx = canvas.getContext("2d");
  if (!ctx) return null;
  if (baseBitmap) {
    ctx.drawImage(baseBitmap, 0, 0, size, size);
  }

  const inset = 3;
  const rect = { x: inset, y: inset, w: size - inset * 2, h: size - inset * 2 };
  const perimeter = 2 * (rect.w + rect.h);
  const start = perimeter * progress;
  const length = perimeter * 0.35;

  ctx.lineWidth = 3.4;
  ctx.lineCap = "round";
  ctx.strokeStyle = color;

  drawPerimeterSegment(ctx, rect, start, length);
  return ctx.getImageData(0, 0, size, size);
}

function drawPerimeterSegment(ctx, rect, start, length) {
  const perimeter = 2 * (rect.w + rect.h);
  let remaining = length;
  let pos = wrap(start, perimeter);

  while (remaining > 0.01) {
    const { point, dir, edgeRemaining } = edgeAt(rect, pos);
    const take = Math.min(remaining, edgeRemaining);
    const p2 = { x: point.x + dir.x * take, y: point.y + dir.y * take };
    ctx.beginPath();
    ctx.moveTo(point.x, point.y);
    ctx.lineTo(p2.x, p2.y);
    ctx.stroke();

    pos = wrap(pos + take, perimeter);
    remaining -= take;
  }
}

function edgeAt(rect, pos) {
  const top = rect.w;
  const right = top + rect.h;
  const bottom = right + rect.w;

  if (pos < top) {
    return { point: { x: rect.x + pos, y: rect.y }, dir: { x: 1, y: 0 }, edgeRemaining: top - pos };
  }
  if (pos < right) {
    const o = pos - top;
    return { point: { x: rect.x + rect.w, y: rect.y + o }, dir: { x: 0, y: 1 }, edgeRemaining: rect.h - o };
  }
  if (pos < bottom) {
    const o = pos - right;
    return { point: { x: rect.x + rect.w - o, y: rect.y + rect.h }, dir: { x: -1, y: 0 }, edgeRemaining: rect.w - o };
  }
  const o = pos - bottom;
  return { point: { x: rect.x, y: rect.y + rect.h - o }, dir: { x: 0, y: -1 }, edgeRemaining: rect.h - o };
}

function wrap(v, max) {
  if (v >= max) return v - max;
  if (v < 0) return v + max;
  return v;
}

async function setIconSafely(options, badgeColor) {
  try {
    const result = await chrome.action.setIcon(options);
    if (chrome.runtime.lastError) {
      throw new Error(chrome.runtime.lastError.message);
    }
    return result;
  } catch (err) {
    console.warn("deskLLM setIcon failed; using badge fallback", err);
    useBadgeFallback = true;
    stopIconAnimation();
    await setBadge("·", badgeColor || "#2ecc71");
    // Once fallback is enabled, avoid further setIcon calls
  }
}

async function setBadge(text, color) {
  try {
    await chrome.action.setBadgeText({ text });
    if (text) {
      await chrome.action.setBadgeBackgroundColor({ color: color || "#2ecc71" });
    }
  } catch (err) {
    console.warn("deskLLM badge update failed", err);
  }
}

// Initialize icon once service worker wakes up
setIconStatus("idle").catch(() => {});
