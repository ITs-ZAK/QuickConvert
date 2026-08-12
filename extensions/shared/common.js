"use strict";

(function expose(root) {
  function isSupportedYoutubeUrl(value) {
    try {
      const url = new URL(value);
      if (url.protocol !== "https:") return false;
      const host = url.hostname.toLowerCase();
      return host === "youtu.be" || host === "youtube.com" || host.endsWith(".youtube.com");
    } catch {
      return false;
    }
  }

  function createDownloadRequest(requestId, url, mediaType, maxResolution) {
    return {
      version: 1,
      requestId,
      operation: "download",
      url,
      mediaType: mediaType.toLowerCase(),
      maxResolution: maxResolution.toLowerCase()
    };
  }

  const api = { isSupportedYoutubeUrl, createDownloadRequest };
  root.QuickConvertCommon = api;
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})(globalThis);
