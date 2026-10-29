import { readFileSync } from 'node:fs';
import { strict as assert } from 'node:assert';
import vm from 'node:vm';

const source = readFileSync(`${process.argv[2]}/src/slic3r/GUI/PrinterWebView.cpp`, 'utf8');
const method = source.slice(source.indexOf('void PrinterWebView::configure_page_scripts()'));
const script = method.match(/AddUserScript\(R"JS\(([\s\S]*?)\)JS"\)/)[1];
function browser(bridgeAtStartup) {
  const received = [], callbacks = [];
  const window = {};
  const bridge = () => { window.wx = { postMessage(value) { received.push(value); } }; };
  if (bridgeAtStartup) bridge();
  const context = vm.createContext({ window, document: { addEventListener(_, fn) { callbacks.push(fn); } }, setTimeout(fn) { callbacks.push(fn); } });
  vm.runInContext(script, context);
  if (!bridgeAtStartup) bridge();
  for (const fn of callbacks) fn();
  const postMessage = window.wx.postMessage;
  vm.runInContext(script, context);
  assert.equal(window.wx.postMessage, postMessage, 'script reinstallation must not wrap the bridge twice');
  window.wx.postMessage({ command: 'sync_mappinp_cfs_filament', data: { slot: 2 } });
  window.wx.postMessage('already serialized');
  assert.deepEqual(JSON.parse(received[0]), { command: 'sync_mappinp_cfs_filament', data: { slot: 2 } });
  assert.equal(received[1], 'already serialized');
}
browser(true);
browser(false);
browser(true); // fresh browser after 2.5 recreation
console.log('PASS: JSON bridge startup, delayed availability, idempotence and recreation.');
