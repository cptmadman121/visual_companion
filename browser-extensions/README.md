TrayVisionPrompt Browser Extensions

Overview
- Chrome and Firefox MV3 extensions add two context menu items on text selection: Proofread and Translate.
- They send the selected text to the local TrayVisionPrompt API at `http://127.0.0.1:27124/v1/process` and replace the selection with the response.

Prerequisites
- Run the TrayVisionPrompt app (Avalonia build). It hosts a local API on 127.0.0.1:27124.
- On Windows, if the API fails to start, you may need a URL ACL:
  - Open an elevated terminal and run:
    - `netsh http add urlacl url=http://127.0.0.1:27124/ user=Everyone`

Chrome (Edge/Chromium)
- Go to `chrome://extensions` and enable Developer mode.
- Click "Load unpacked" and select `browser-extensions/chrome`.
- Highlight some text, right-click, choose a TrayVisionPrompt item.

Firefox
- Go to `about:debugging#/runtime/this-firefox`.
- Click "Load Temporary Add-on" and select the `browser-extensions/firefox/manifest.json`.
- Highlight text, right-click → TrayVisionPrompt.

Notes
- Extensions require host permissions for `http://127.0.0.1:27124/*` (declared in manifests).
- If replacing selection fails in a particular site, the extension falls back to Range insertion.
- You can customize behavior by changing the `action` (`proofread`, `translate`) or sending a custom prompt from the extension to the API.

