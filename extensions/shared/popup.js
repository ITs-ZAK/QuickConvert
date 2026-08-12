"use strict";

const extensionApi = globalThis.browser ?? globalThis.chrome;
const titleElement = document.querySelector("#title");
const statusElement = document.querySelector("#status");
const mediaTypeElement = document.querySelector("#mediaType");
const qualityElement = document.querySelector("#quality");
const downloadButton = document.querySelector("#download");
let currentTab;

const messages = {
  accepted: "Dodano do kolejki QuickConvert.",
  app_unavailable: "Zainstaluj lub uruchom aplikację QuickConvert.",
  invalid_request: "Nieprawidłowe ustawienia pobierania.",
  unsupported_url: "Ta strona nie zawiera obsługiwanego filmu.",
  unauthorized_caller: "Rozszerzenie nie ma dostępu do aplikacji."
};

initialize().catch(() => showStatus("Nie udało się odczytać bieżącej karty.", true));

async function initialize() {
  [currentTab] = await extensionApi.tabs.query({ active: true, currentWindow: true });
  titleElement.textContent = currentTab?.title || "Bieżący film";
  const supported = QuickConvertCommon.isSupportedYoutubeUrl(currentTab?.url || "");
  downloadButton.disabled = !supported;
  if (!supported) showStatus("Otwórz film YouTube albo Shorts.", true);

  const stored = await extensionApi.storage.local.get(["mediaType", "quality"]);
  mediaTypeElement.value = stored.mediaType || "mp3";
  qualityElement.value = stored.quality || "best";
  updateQualityState();
}

mediaTypeElement.addEventListener("change", updateQualityState);
downloadButton.addEventListener("click", async () => {
  if (!currentTab || !QuickConvertCommon.isSupportedYoutubeUrl(currentTab.url)) return;
  downloadButton.disabled = true;
  showStatus("Przekazywanie do aplikacji…", false);

  const mediaType = mediaTypeElement.value;
  const quality = mediaType === "mp3" ? "best" : qualityElement.value;
  await extensionApi.storage.local.set({ mediaType, quality });
  const requestId = globalThis.crypto.randomUUID();
  const payload = QuickConvertCommon.createDownloadRequest(
    requestId, currentTab.url, mediaType, quality);

  try {
    const response = await extensionApi.runtime.sendMessage({ action: "download", payload });
    showStatus(messages[response?.code] || "Nieznany błąd aplikacji.", response?.code !== "accepted");
  } catch {
    showStatus(messages.app_unavailable, true);
  } finally {
    downloadButton.disabled = false;
  }
});

function updateQualityState() {
  qualityElement.disabled = mediaTypeElement.value === "mp3";
}

function showStatus(message, isError) {
  statusElement.textContent = message;
  statusElement.classList.toggle("error", isError);
}
