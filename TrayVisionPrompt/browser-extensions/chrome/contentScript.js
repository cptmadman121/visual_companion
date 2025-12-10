const STYLE_ID = "deskllm-styles";
let lastSelectionContext = null;

async function readClipboardTextSafe() {
  try {
    if (!navigator.clipboard?.readText) return null;
    return await navigator.clipboard.readText();
  } catch {
    return null;
  }
}

async function restoreClipboardTextSafe(previousText) {
  if (typeof previousText !== "string") return;
  try {
    await navigator.clipboard.writeText(previousText);
  } catch {
    /* ignore clipboard restore errors */
  }
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  switch (message?.type) {
    case "deskllm/get-selection":
      sendResponse({ text: readSelection() });
      return;
    case "deskllm/replace-selection":
      sendResponse({ ok: replaceSelection(message.text || "") });
      return;
    case "deskllm/show-response":
      showResponse(message.title, message.text, message.allowReplace);
      sendResponse({ ok: true });
      return;
    case "deskllm/show-toast":
      showToast(message.message);
      sendResponse({ ok: true });
      return;
    case "deskllm/request-text":
      requestText(message.prompt, message.prefill).then((text) => sendResponse({ text }));
      return true; // keep channel open for async response
    default:
      return;
  }
});

function isTextInput(el) {
  if (!el) return false;
  if (el.tagName === "TEXTAREA") return true;
  if (el.tagName === "INPUT") {
    return /^(text|search|url|email|number|tel)$/i.test(el.type || "");
  }
  return false;
}

function captureSelectionContext() {
  const active = getDeepActiveElement();
  if (isTextInput(active)) {
    return {
      type: "input",
      element: active,
      start: typeof active.selectionStart === "number" ? active.selectionStart : 0,
      end: typeof active.selectionEnd === "number" ? active.selectionEnd : (active.value?.length ?? 0),
      scrollTop: active.scrollTop,
      scrollLeft: active.scrollLeft
    };
  }

  const selection = window.getSelection();
  if (selection && selection.rangeCount > 0) {
    return { type: "range", range: selection.getRangeAt(0).cloneRange() };
  }
  return null;
}

function restoreSelectionContext() {
  if (!lastSelectionContext) {
    return { active: getDeepActiveElement(), selection: window.getSelection() };
  }

  if (lastSelectionContext.type === "input" && lastSelectionContext.element?.isConnected) {
    const el = lastSelectionContext.element;
    try {
      el.focus?.({ preventScroll: true });
      if (typeof lastSelectionContext.start === "number" && typeof lastSelectionContext.end === "number") {
        el.selectionStart = lastSelectionContext.start;
        el.selectionEnd = lastSelectionContext.end;
      }
      if (typeof lastSelectionContext.scrollTop === "number") el.scrollTop = lastSelectionContext.scrollTop;
      if (typeof lastSelectionContext.scrollLeft === "number") el.scrollLeft = lastSelectionContext.scrollLeft;
    } catch { /* ignore restore errors */ }
    return { active: el, selection: window.getSelection() };
  }

  if (lastSelectionContext.type === "range" && lastSelectionContext.range) {
    const sel = window.getSelection();
    if (sel) {
      try {
        sel.removeAllRanges();
        sel.addRange(lastSelectionContext.range);
      } catch { /* ignore selection errors */ }
    }
    const focusNode = lastSelectionContext.range.commonAncestorContainer;
    const focusEl = focusNode?.nodeType === Node.ELEMENT_NODE ? focusNode : focusNode?.parentElement;
    try {
      focusEl?.focus?.({ preventScroll: true });
    } catch { /* ignore focus errors */ }
    return { active: getDeepActiveElement(), selection: window.getSelection() };
  }

  return { active: getDeepActiveElement(), selection: window.getSelection() };
}

function ensureStyles() {
  if (document.getElementById(STYLE_ID)) return;
  const style = document.createElement("style");
  style.id = STYLE_ID;
  style.textContent = `
    #deskllm-overlay {
      position: fixed;
      inset: 0;
      background: rgba(5, 7, 11, 0.55);
      backdrop-filter: blur(4px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 2147483647;
      font-family: "Inter", "Segoe UI", system-ui, sans-serif;
    }
    .deskllm-panel {
      width: min(640px, 90vw);
      max-height: 80vh;
      background: #0f1729;
      color: #e8edf8;
      border-radius: 14px;
      box-shadow: 0 18px 60px rgba(0, 0, 0, 0.45), 0 0 0 1px rgba(255, 255, 255, 0.04);
      border: 1px solid rgba(255, 255, 255, 0.08);
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
    .deskllm-panel header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 14px 16px;
      background: linear-gradient(135deg, #162034, #11182b);
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      font-weight: 600;
    }
    .deskllm-panel header button {
      background: transparent;
      border: none;
      color: #9fb4ff;
      font-size: 18px;
      cursor: pointer;
    }
    .deskllm-panel pre,
    .deskllm-panel textarea {
      margin: 0;
      padding: 16px;
      font-family: "JetBrains Mono", "SFMono-Regular", Consolas, monospace;
      background: #0b1220;
      color: #e8edf8;
      border: none;
      outline: none;
      width: 100%;
      flex: 1;
      resize: vertical;
      min-height: 180px;
      line-height: 1.4;
      white-space: pre-wrap;
    }
    .deskllm-panel .actions {
      display: flex;
      gap: 8px;
      padding: 12px 14px;
      background: #0e1524;
      border-top: 1px solid rgba(255, 255, 255, 0.05);
    }
    .deskllm-panel button.action {
      background: linear-gradient(135deg, #2f7bff, #5ea3ff);
      border: none;
      color: #0b1020;
      font-weight: 600;
      padding: 10px 14px;
      border-radius: 10px;
      cursor: pointer;
      box-shadow: 0 10px 30px rgba(47, 123, 255, 0.35);
    }
    .deskllm-panel button.secondary {
      background: rgba(255, 255, 255, 0.06);
      color: #cbd7ff;
      border: 1px solid rgba(255, 255, 255, 0.08);
      box-shadow: none;
    }
    #deskllm-toast-container {
      position: fixed;
      bottom: 20px;
      right: 20px;
      display: flex;
      flex-direction: column;
      gap: 8px;
      z-index: 2147483647;
      font-family: "Inter", "Segoe UI", system-ui, sans-serif;
    }
    .deskllm-toast {
      background: #0f1729;
      color: #e8edf8;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 10px;
      padding: 10px 14px;
      box-shadow: 0 12px 30px rgba(0,0,0,0.35);
      min-width: 220px;
      max-width: 360px;
      animation: deskllm-fade-in 150ms ease-out;
    }
    @keyframes deskllm-fade-in {
      from { opacity: 0; transform: translateY(6px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `;
  document.head.appendChild(style);
}

function readSelection() {
  lastSelectionContext = captureSelectionContext();
  const selection = window.getSelection();
  if (selection && selection.toString()) {
    return selection.toString();
  }

  const active = getDeepActiveElement();
  if (active) {
    if (active.value) return active.value.substring(active.selectionStart || 0, active.selectionEnd || active.value.length) || active.value;
    if (active.innerText) return active.innerText;
  }
  return "";
}

function replaceSelection(text) {
  if (!text && text !== "") return false;
  let { active, selection } = (() => {
    const currentActive = getDeepActiveElement();
    const currentSelection = window.getSelection();
    if (isTextInput(currentActive) || (currentSelection && currentSelection.rangeCount > 0)) {
      return { active: currentActive, selection: currentSelection };
    }
    return restoreSelectionContext();
  })();

  if (isTextInput(active)) {
    try {
      // Prefer native setRangeText when available (works even without focus in some widgets).
      if (typeof active.setRangeText === "function") {
        const start = typeof active.selectionStart === "number" ? active.selectionStart : 0;
        const end = typeof active.selectionEnd === "number" ? active.selectionEnd : active.value.length;
        active.setRangeText(text, start, end, "end");
      } else {
        const start = typeof active.selectionStart === "number" ? active.selectionStart : 0;
        const end = typeof active.selectionEnd === "number" ? active.selectionEnd : active.value.length;
        const before = active.value.slice(0, start);
        const after = active.value.slice(end);
        active.value = `${before}${text}${after}`;
      }
      const caret = (typeof active.selectionStart === "number" ? active.selectionStart : active.value.length);
      try {
        active.focus();
        active.selectionStart = active.selectionEnd = caret;
      } catch { /* ignore caret issues */ }
      active.dispatchEvent(new Event("input", { bubbles: true }));
      lastSelectionContext = null;
      return true;
    } catch {
      // fall through to other strategies
    }
  }

  if ((!selection || selection.rangeCount === 0)) {
    const restored = restoreSelectionContext();
    selection = restored.selection;
    active = restored.active || active;
  }

  if (selection && selection.rangeCount > 0) {
    const range = selection.getRangeAt(0);
    range.deleteContents();
    range.insertNode(document.createTextNode(text));
    selection.removeAllRanges();
    lastSelectionContext = null;
    return true;
  }

  try {
    const ok = document.execCommand("insertText", false, text);
    if (ok) {
      lastSelectionContext = null;
      return true;
    }
  } catch { /* ignore */ }

  // Last resort: paste via clipboard (fire and forget to keep return type boolean) and restore prior clipboard if possible.
  try {
    const prevActive = getDeepActiveElement();
    const previousClipboardPromise = readClipboardTextSafe();

    navigator.clipboard?.writeText(text).then(() => {
      try { document.execCommand("paste"); } catch { /* ignore */ }
      if (prevActive && typeof prevActive.dispatchEvent === "function") {
        prevActive.dispatchEvent(new Event("input", { bubbles: true }));
      }
    }).catch(() => {}).finally(() => {
      previousClipboardPromise.then((previous) => restoreClipboardTextSafe(previous));
    });

    lastSelectionContext = null;
    return true;
  } catch {
    return false;
  }
}

function showResponse(title, text, allowReplace) {
  lastSelectionContext = allowReplace ? captureSelectionContext() : null;
  ensureStyles();
  removeOverlay();

  const overlay = document.createElement("div");
  overlay.id = "deskllm-overlay";

  const panel = document.createElement("div");
  panel.className = "deskllm-panel";

  const header = document.createElement("header");
  header.textContent = title || "deskLLM";
  const close = document.createElement("button");
  close.textContent = "✕";
  close.onclick = removeOverlay;
  header.appendChild(close);

  const body = document.createElement("pre");
  body.textContent = text || "";

  const actions = document.createElement("div");
  actions.className = "actions";

  if (allowReplace) {
    const apply = document.createElement("button");
    apply.className = "action";
    apply.textContent = "Replace selection";
    apply.onclick = () => {
      replaceSelection(text || "");
      removeOverlay();
    };
    actions.appendChild(apply);
  }

  const copy = document.createElement("button");
  copy.className = "action secondary";
  copy.textContent = "Copy";
  copy.onclick = async () => {
    try {
      await navigator.clipboard.writeText(text || "");
      showToast("Copied to clipboard.");
    } catch {
      showToast("Clipboard copy failed.");
    }
  };

  const dismiss = document.createElement("button");
  dismiss.className = "action secondary";
  dismiss.textContent = "Close";
  dismiss.onclick = removeOverlay;

  actions.append(copy, dismiss);
  panel.append(header, body, actions);
  overlay.appendChild(panel);
  document.body.appendChild(overlay);
}

function removeOverlay() {
  const overlay = document.getElementById("deskllm-overlay");
  if (overlay) overlay.remove();
}

function getDeepActiveElement() {
  let el = document.activeElement;
  while (el && el.shadowRoot && el.shadowRoot.activeElement && el.shadowRoot.activeElement !== el) {
    el = el.shadowRoot.activeElement;
  }
  return el;
}

function showToast(message) {
  if (!message) return;
  ensureStyles();

  let container = document.getElementById("deskllm-toast-container");
  if (!container) {
    container = document.createElement("div");
    container.id = "deskllm-toast-container";
    document.body.appendChild(container);
  }

  const toast = document.createElement("div");
  toast.className = "deskllm-toast";
  toast.textContent = message;
  container.appendChild(toast);

  setTimeout(() => {
    toast.style.opacity = "0";
    setTimeout(() => toast.remove(), 300);
  }, 3000);
}

function requestText(prompt, prefill) {
  ensureStyles();
  removeOverlay();

  return new Promise((resolve) => {
    const overlay = document.createElement("div");
    overlay.id = "deskllm-overlay";

    const panel = document.createElement("div");
    panel.className = "deskllm-panel";

    const header = document.createElement("header");
    header.textContent = prompt || "deskLLM prompt";
    const close = document.createElement("button");
    close.textContent = "✕";
    close.onclick = () => {
      removeOverlay();
      resolve("");
    };
    header.appendChild(close);

    const textarea = document.createElement("textarea");
    textarea.value = prefill || "";
    textarea.placeholder = "Type the text to send to deskLLM...";

    const actions = document.createElement("div");
    actions.className = "actions";

    const send = document.createElement("button");
    send.className = "action";
    send.textContent = "Send";
    send.onclick = () => {
      const value = textarea.value || "";
      removeOverlay();
      resolve(value);
    };

    const cancel = document.createElement("button");
    cancel.className = "action secondary";
    cancel.textContent = "Cancel";
    cancel.onclick = () => {
      removeOverlay();
      resolve("");
    };

    actions.append(send, cancel);
    panel.append(header, textarea, actions);
    overlay.appendChild(panel);
    document.body.appendChild(overlay);
    textarea.focus();
  });
}
