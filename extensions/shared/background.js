"use strict";

const unavailable = () => ({ code: "app_unavailable" });
const isDownload = message => Boolean(message && message.action === "download");

function createFirefoxListener(browserApi) {
  return message => {
    if (!isDownload(message)) return false;
    return browserApi.runtime
      .sendNativeMessage("com.quickconvert.app", message.payload)
      .then(response => response ?? unavailable())
      .catch(unavailable);
  };
}

function createChromeListener(chromeApi) {
  return (message, _sender, sendResponse) => {
    if (!isDownload(message)) return false;
    chromeApi.runtime
      .sendNativeMessage("com.quickconvert.app", message.payload)
      .then(response => sendResponse(response ?? unavailable()))
      .catch(() => sendResponse(unavailable()));
    return true;
  };
}

if (globalThis.browser) {
  browser.runtime.onMessage.addListener(createFirefoxListener(browser));
} else {
  chrome.runtime.onMessage.addListener(createChromeListener(chrome));
}
