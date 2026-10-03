// Minimal browser WebSocket bridge. C# polls it every frame (no callbacks into C#).
mergeInto(LibraryManager.library, {
  KR_Connect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (window.__kr && window.__kr.ws) { try { window.__kr.ws.close(); } catch (e) {} }
    var w = window.__kr = { ws: null, queue: [], state: 0 };
    try {
      var ws = new WebSocket(url);
      w.ws = ws;
      ws.onopen = function () { if (window.__kr === w) w.state = 1; };
      ws.onmessage = function (e) { if (window.__kr === w) w.queue.push(String(e.data)); };
      ws.onclose = function () { if (window.__kr === w) w.state = 3; };
      ws.onerror = function () { if (window.__kr === w) w.state = 3; };
    } catch (e) { w.state = 3; }
  },
  KR_State: function () {
    return window.__kr ? window.__kr.state : 3;
  },
  KR_Send: function (msgPtr) {
    var w = window.__kr;
    if (w && w.state === 1) { try { w.ws.send(UTF8ToString(msgPtr)); } catch (e) {} }
  },
  KR_Recv: function () {
    var w = window.__kr;
    if (!w || w.queue.length === 0) return 0;
    var s = w.queue.shift();
    var len = lengthBytesUTF8(s) + 1;
    var buf = _malloc(len);
    stringToUTF8(s, buf, len);
    return buf;
  },
  KR_Close: function () {
    var w = window.__kr;
    if (w && w.ws) { try { w.ws.close(); } catch (e) {} }
    window.__kr = null;
  }
});
