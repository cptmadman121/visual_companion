const API = "http://127.0.0.1:27124/v1/process";

browser.runtime.onInstalled.addListener(() => {
  browser.contextMenus.create({ id: "tvp-proofread", title: "TrayVisionPrompt: Proofread", contexts: ["selection"] });
  browser.contextMenus.create({ id: "tvp-translate", title: "TrayVisionPrompt: Translate", contexts: ["selection"] });
});

browser.contextMenus.onClicked.addListener(async (info, tab) => {
  if (!tab || !tab.id) return;
  const action = info.menuItemId === "tvp-translate" ? "translate" : "proofread";

  const [{ result }] = await browser.scripting.executeScript({
    target: { tabId: tab.id },
    func: () => window.getSelection()?.toString() || ""
  });

  const text = result || "";
  if (!text.trim()) {
    return;
  }

  try {
    const resp = await fetch(API, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action, text })
    });
    const data = await resp.json();
    if (!resp.ok) throw new Error(data?.error || `HTTP ${resp.status}`);
    const newText = data.response || "";
    await browser.scripting.executeScript({
      target: { tabId: tab.id },
      args: [newText],
      func: (replacement) => {
        const sel = window.getSelection();
        if (!sel || !replacement) return;
        try {
          document.execCommand("insertText", false, replacement);
        } catch (_) {
          const range = sel.rangeCount ? sel.getRangeAt(0) : null;
          if (!range) return;
          range.deleteContents();
          range.insertNode(document.createTextNode(replacement));
        }
      }
    });
  } catch (e) {
    console.error("TrayVisionPrompt error:", e);
  }
});

