const WebSocket = require('ws');
const assert = require('assert');
const { server } = require('./kart-server');
function client(port) {
  const ws = new WebSocket('ws://localhost:' + port); const msgs = []; const waiters = [];
  ws.on('message', d => { const m = JSON.parse(d.toString()); msgs.push(m); for (let i = waiters.length - 1; i >= 0; i--) if (waiters[i].t === m.t) { waiters[i].res(m); waiters.splice(i, 1); } });
  return { ws, msgs, open: () => new Promise(r => ws.on('open', r)), send: o => ws.send(JSON.stringify(o)),
    wait: (t, ms = 3000) => { const f = msgs.find(m => m.t === t); if (f) { msgs.splice(msgs.indexOf(f), 1); return Promise.resolve(f); } return new Promise((res, rej) => { waiters.push({ t, res }); setTimeout(() => rej(new Error('timeout ' + t)), ms); }); } };
}
const sleep = ms => new Promise(r => setTimeout(r, ms));
server.listen(0, async () => {
  const port = server.address().port;
  try {
    const a = client(port), b = client(port); await a.open(); await b.open();
    a.send({ t: 'create', name: 'Ann' }); const ja = await a.wait('joined'); const room = await a.wait('room');
    b.send({ t: 'join', code: room.code, name: 'Bob' }); const jb = await b.wait('joined'); await b.wait('room');
    b.send({ t: 'start' }); await sleep(100); assert(!a.msgs.find(m => m.t === 'start'), 'non-host start');
    a.send({ t: 'track', track: 7 }); await sleep(100);
    a.send({ t: 'start' });
    const st = await a.wait('start'); assert.strictEqual(st.track, 7); assert.strictEqual(st.slots.length, 2);
    a.send({ t: 'state', x: 1, z: 2, h: 0.5, v: 20, d: 1, b: 0, s: 0, sh: 0, c: 1, i: 10 }); await sleep(200);
    const snap = await b.wait('snap'); 
    let found = false; for (let i = 0; i < 6 && !found; i++) { const s = await b.wait('snap'); found = s.p.some(p => p.id === ja.id && p.x === 1); }
    assert(found, 'state relayed');
    a.send({ t: 'slick', x: 3, z: 4 }); const sl = await b.wait('slick'); assert.strictEqual(sl.x, 3);
    a.send({ t: 'hit', target: jb.id }); await b.wait('hit');
    a.send({ t: 'finish', time: 61.5 }); await b.wait('finished');
    b.send({ t: 'finish', time: 70 });
    const res = await a.wait('results'); assert.strictEqual(res.list[0].id, ja.id); assert.strictEqual(res.list.length, 2);
    console.log('private race ok');
    const c = client(port), d = client(port); await c.open(); await d.open();
    c.send({ t: 'joinpublic', name: 'Cy' }); await c.wait('joined'); d.send({ t: 'joinpublic', name: 'Di' }); await d.wait('joined');
    console.log('public join ok');
    console.log('ALL TESTS PASSED'); process.exit(0);
  } catch (e) { console.error('FAIL', e); process.exit(1); }
});
