"use strict";

const assert = require("node:assert/strict");
const common = require("../shared/common.js");

assert.equal(common.isSupportedYoutubeUrl("https://www.youtube.com/watch?v=abc"), true);
assert.equal(common.isSupportedYoutubeUrl("https://youtu.be/abc?list=playlist"), true);
assert.equal(common.isSupportedYoutubeUrl("https://youtube.com.evil.test/watch?v=abc"), false);
assert.equal(common.isSupportedYoutubeUrl("http://youtube.com/watch?v=abc"), false);

assert.deepEqual(
  common.createDownloadRequest("req-1", "https://youtu.be/abc", "MP3", "BEST"),
  {
    version: 1,
    requestId: "req-1",
    operation: "download",
    url: "https://youtu.be/abc",
    mediaType: "mp3",
    maxResolution: "best"
  }
);

console.log("PASS: extension common tests");
