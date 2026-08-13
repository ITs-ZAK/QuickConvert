"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const backgroundSource = fs.readFileSync(
  path.join(__dirname, "..", "shared", "background.js"),
  "utf8");
const download = { action: "download", payload: { requestId: "req-1" } };

function loadFirefox(sendNativeMessage) {
  let listener;
  const browser = {
    runtime: {
      onMessage: { addListener(value) { listener = value; } },
      sendNativeMessage
    }
  };
  vm.runInNewContext(backgroundSource, { browser, Promise });
  return listener;
}

function loadChrome(sendNativeMessage) {
  let listener;
  const chrome = {
    runtime: {
      onMessage: { addListener(value) { listener = value; } },
      sendNativeMessage
    }
  };
  vm.runInNewContext(backgroundSource, { chrome, Promise });
  return listener;
}

async function run() {
  const acceptedFirefox = loadFirefox(() => Promise.resolve({ code: "accepted" }));
  assert.deepEqual(
    JSON.parse(JSON.stringify(await acceptedFirefox(download))),
    { code: "accepted" });

  const rejectedFirefox = loadFirefox(() => Promise.reject(new Error("host missing")));
  assert.deepEqual(
    JSON.parse(JSON.stringify(await rejectedFirefox(download))),
    { code: "app_unavailable" });

  const emptyFirefox = loadFirefox(() => Promise.resolve(null));
  assert.deepEqual(
    JSON.parse(JSON.stringify(await emptyFirefox(download))),
    { code: "app_unavailable" });
  assert.equal(acceptedFirefox({ action: "ping" }), false);

  let chromeResponse;
  const acceptedChrome = loadChrome(() => Promise.resolve({ code: "accepted" }));
  assert.equal(acceptedChrome(download, {}, value => { chromeResponse = value; }), true);
  await Promise.resolve();
  assert.deepEqual(chromeResponse, { code: "accepted" });

  let rejectedResponse;
  const rejectedChrome = loadChrome(() => Promise.reject(new Error("host missing")));
  assert.equal(rejectedChrome(download, {}, value => { rejectedResponse = value; }), true);
  await new Promise(resolve => setImmediate(resolve));
  assert.deepEqual(
    JSON.parse(JSON.stringify(rejectedResponse)),
    { code: "app_unavailable" });

  let emptyResponse;
  const emptyChrome = loadChrome(() => Promise.resolve(null));
  assert.equal(emptyChrome(download, {}, value => { emptyResponse = value; }), true);
  await Promise.resolve();
  assert.deepEqual(
    JSON.parse(JSON.stringify(emptyResponse)),
    { code: "app_unavailable" });

  let unrelatedNativeCalls = 0;
  const unrelatedChrome = loadChrome(() => {
    unrelatedNativeCalls++;
    return Promise.resolve({ code: "accepted" });
  });
  assert.equal(unrelatedChrome({ action: "ping" }, {}, () => {}), false);
  assert.equal(unrelatedNativeCalls, 0);

  console.log("PASS: extension background tests");
}

run().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
