"use strict";

const extensionApi = globalThis.browser ?? globalThis.chrome;

extensionApi.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (!message || message.action !== "download") return false;

  extensionApi.runtime
    .sendNativeMessage("com.quickconvert.app", message.payload)
    .then(response => sendResponse(response ?? { code: "app_unavailable" }))
    .catch(() => sendResponse({ code: "app_unavailable" }));
  return true;
});
