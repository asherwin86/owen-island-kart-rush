// Island Kart Rush online server.
// Rooms with 4-letter codes plus one always-open public room. Clients simulate their own kart and
// stream its state; the server relays snapshots, hands out colours/slots, and decides the final order.
const http = require('http');
const { WebSocketServer } = require('ws');

const PORT = process.env.PORT || 8080;
const TICK_HZ = 15;
const MAX_PLAYERS = 8;
const TRACK_COUNT = 20;
const COUNTDOWN_MS = 3500;
const FINISH_GRACE_MS = 25000;     // after the first finisher, everyone else gets this long
const RACE_CAP_MS = 6 * 60 * 1000;
const PUBLIC_CODE = 'PUBLIC';
const PUBLIC_COUNTDOWN_MS = 15000;
const PUBLIC_BREAK_MS = 12000;
const CODE_LETTERS = 'ABCDEFGHJKLMNPQRSTUVWXYZ';

const rooms = new Map();
let nextId = 1;

const server = http.createServer((req, res) => {
  res.writeHead(200, { 'Content-Type': 'text/plain' });
  res.end('Island Kart Rush server is running. Rooms: ' + rooms.size + '\n');
});
const wss = new WebSocketServer({ server });

function makeCode() {
  for (let a = 0; a < 100; a++) {
    let c = '';
    for (let i = 0; i < 4; i++) c += CODE_LETTERS[Math.floor(Math.random() * CODE_LETTERS.length)];
    if (!rooms.has(c)) return c;
  }
  return null;
}
function cleanName(n) {
  const s = String(n || '').replace(/[^\w \-]/g, '').trim().slice(0, 12);
  return s || 'Racer' + Math.floor(100 + Math.random() * 900);
}
function send(ws, o) { if (ws.readyState === 1) ws.send(JSON.stringify(o)); }
function broadcast(room, o, except) {
  const d = JSON.stringify(o);
  for (const p of room.players.values()) if (p !== except && p.ws.readyState === 1) p.ws.send(d);
}
function roomInfo(room) {
  return {
    t: 'room', code: room.code, hostId: room.hostId, isPublic: room.isPublic, phase: room.phase, track: room.track,
    players: [...room.players.values()].map(p => ({ id: p.id, name: p.name, color: p.color })),
  };
}

function createRoom(host, isPublic) {
  const code = isPublic ? PUBLIC_CODE : makeCode();
  if (!code) return null;
  const room = {
    code, isPublic: !!isPublic, hostId: isPublic ? 0 : host.id, phase: 'lobby', track: Math.floor(Math.random() * TRACK_COUNT),
    players: new Map(), nextStart: 0, raceStart: 0, firstFinish: 0, finishOrder: [], tick: null,
  };
  rooms.set(code, room);
  room.tick = setInterval(() => tickRoom(room), 1000 / TICK_HZ);
  return room;
}

function addPlayer(room, p) {
  const used = new Set([...room.players.values()].map(o => o.color));
  p.color = 0; while (used.has(p.color)) p.color++;
  p.room = room; p.st = null; p.finished = false; p.time = 0;
  room.players.set(p.id, p);
}

function removePlayer(p) {
  const room = p.room;
  if (!room) return;
  room.players.delete(p.id);
  p.room = null;
  if (room.players.size === 0) { clearInterval(room.tick); rooms.delete(room.code); return; }
  if (!room.isPublic && room.hostId === p.id) room.hostId = room.players.keys().next().value;
  broadcast(room, roomInfo(room));
  if (room.phase === 'racing') checkAllFinished(room);
}

function startRace(room) {
  if (room.isPublic) room.track = Math.floor(Math.random() * TRACK_COUNT);
  room.phase = 'racing';
  room.nextStart = 0;
  room.raceStart = Date.now() + COUNTDOWN_MS;
  room.firstFinish = 0;
  room.finishOrder = [];
  const ids = [...room.players.keys()];
  for (let i = ids.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [ids[i], ids[j]] = [ids[j], ids[i]]; }
  ids.forEach(id => { const p = room.players.get(id); p.slot = ids.indexOf(id); p.finished = false; p.st = null; });
  broadcast(room, {
    t: 'start', track: room.track, countdown: COUNTDOWN_MS / 1000,
    slots: [...room.players.values()].map(p => ({ id: p.id, slot: p.slot })),
  });
  broadcast(room, roomInfo(room));
}

function endRace(room) {
  if (room.phase !== 'racing') return;
  room.phase = 'lobby';
  if (room.isPublic) room.nextStart = Date.now() + PUBLIC_BREAK_MS;
  const unfinished = [...room.players.values()].filter(p => !p.finished)
    .sort((a, b) => ((b.st ? b.st.c * 1000 + b.st.i : 0) - (a.st ? a.st.c * 1000 + a.st.i : 0)));
  const list = room.finishOrder.map(p => ({ id: p.id, name: p.name, time: p.time }))
    .concat(unfinished.map(p => ({ id: p.id, name: p.name, time: -1 })));
  broadcast(room, { t: 'results', list });
  broadcast(room, roomInfo(room));
}

function checkAllFinished(room) {
  if (room.phase !== 'racing') return;
  const all = [...room.players.values()];
  if (all.length === 0 || all.every(p => p.finished)) endRace(room);
}

function tickRoom(room) {
  const now = Date.now();
  const snap = [];
  for (const p of room.players.values()) if (p.st) snap.push(Object.assign({ id: p.id }, p.st));
  broadcast(room, { t: 'snap', p: snap, next: room.isPublic && room.phase === 'lobby' && room.nextStart ? Math.max(0, (room.nextStart - now) / 1000) : undefined });

  if (room.phase === 'racing') {
    if (room.firstFinish && now - room.firstFinish > FINISH_GRACE_MS) endRace(room);
    else if (now - room.raceStart > RACE_CAP_MS) endRace(room);
  } else if (room.isPublic) {
    if (room.players.size >= 2) {
      if (!room.nextStart) room.nextStart = now + PUBLIC_COUNTDOWN_MS;
      if (now >= room.nextStart) startRace(room);
    } else room.nextStart = 0;
  }
}

wss.on('connection', (ws) => {
  const p = { id: nextId++, ws, name: '', room: null };
  ws.isAlive = true;
  ws.on('pong', () => { ws.isAlive = true; });
  ws.on('message', (raw) => {
    let m; try { m = JSON.parse(raw.toString()); } catch { return; }
    if (!m || typeof m.t !== 'string') return;
    switch (m.t) {
      case 'create': {
        if (p.room) return;
        p.name = cleanName(m.name);
        const room = createRoom(p, false);
        if (!room) { send(ws, { t: 'err', msg: 'Could not create a room, try again.' }); return; }
        addPlayer(room, p); send(ws, { t: 'joined', id: p.id }); broadcast(room, roomInfo(room));
        break;
      }
      case 'joinpublic': {
        if (p.room) return;
        const room = rooms.get(PUBLIC_CODE) || createRoom(p, true);
        if (room.players.size >= MAX_PLAYERS) { send(ws, { t: 'err', msg: 'The public race is full, try again soon.' }); return; }
        if (room.phase === 'racing') { send(ws, { t: 'err', msg: 'A race is in progress - try again in a moment.' }); return; }
        p.name = cleanName(m.name);
        addPlayer(room, p); send(ws, { t: 'joined', id: p.id }); broadcast(room, roomInfo(room));
        break;
      }
      case 'join': {
        if (p.room) return;
        const code = String(m.code || '').toUpperCase().trim();
        if (code === PUBLIC_CODE) { send(ws, { t: 'err', msg: 'Use the public race button for that one.' }); return; }
        const room = rooms.get(code);
        if (!room) { send(ws, { t: 'err', msg: 'No room with that code.' }); return; }
        if (room.players.size >= MAX_PLAYERS) { send(ws, { t: 'err', msg: 'That room is full.' }); return; }
        if (room.phase === 'racing') { send(ws, { t: 'err', msg: 'That race already started.' }); return; }
        p.name = cleanName(m.name);
        addPlayer(room, p); send(ws, { t: 'joined', id: p.id }); broadcast(room, roomInfo(room));
        break;
      }
      case 'track': {
        const room = p.room;
        if (!room || room.isPublic || room.hostId !== p.id || room.phase !== 'lobby') return;
        const n = Math.floor(Number(m.track));
        if (n >= 0 && n < TRACK_COUNT) { room.track = n; broadcast(room, roomInfo(room)); }
        break;
      }
      case 'start': {
        const room = p.room;
        if (!room || room.isPublic || room.hostId !== p.id || room.phase !== 'lobby') return;
        if (room.players.size < 2) { send(ws, { t: 'err', msg: 'Need at least 2 racers to start.' }); return; }
        startRace(room);
        break;
      }
      case 'state': {
        const room = p.room;
        if (!room || room.phase !== 'racing') return;
        const f = ['x', 'z', 'h', 'v'];
        if (!f.every(k => Number.isFinite(m[k]))) return;
        p.st = { x: m.x, z: m.z, h: m.h, v: m.v, d: m.d | 0, b: m.b | 0, s: m.s | 0, sh: m.sh | 0, c: m.c | 0, i: m.i | 0 };
        break;
      }
      case 'finish': {
        const room = p.room;
        if (!room || room.phase !== 'racing' || p.finished) return;
        p.finished = true;
        p.time = Math.max(0, Number(m.time) || (Date.now() - room.raceStart) / 1000);
        room.finishOrder.push(p);
        if (!room.firstFinish) room.firstFinish = Date.now();
        broadcast(room, { t: 'finished', id: p.id, name: p.name });
        checkAllFinished(room);
        break;
      }
      case 'slick': {
        const room = p.room;
        if (!room || room.phase !== 'racing' || !Number.isFinite(m.x) || !Number.isFinite(m.z)) return;
        broadcast(room, { t: 'slick', x: m.x, z: m.z, by: p.id }, p);
        break;
      }
      case 'hit': {
        const room = p.room;
        if (!room || room.phase !== 'racing') return;
        const target = room.players.get(Number(m.target));
        if (target && target !== p) send(target.ws, { t: 'hit', by: p.id });
        break;
      }
      case 'kick': {
        const room = p.room;
        if (!room || room.isPublic || room.hostId !== p.id) return;
        const target = room.players.get(Number(m.id));
        if (!target || target.id === p.id) return;
        send(target.ws, { t: 'kicked' });
        removePlayer(target);
        break;
      }
      case 'leave': removePlayer(p); break;
    }
  });
  ws.on('close', () => removePlayer(p));
  ws.on('error', () => {});
});

setInterval(() => {
  for (const ws of wss.clients) {
    if (!ws.isAlive) { ws.terminate(); continue; }
    ws.isAlive = false; ws.ping();
  }
}, 20000);

if (require.main === module) {
  server.listen(PORT, () => {
    console.log('Island Kart Rush server listening on port ' + PORT);
    console.log('  On this computer use:  ws://localhost:' + PORT);
    const nets = require('os').networkInterfaces();
    for (const name of Object.keys(nets)) for (const n of nets[name])
      if (n.family === 'IPv4' && !n.internal) console.log('  Friends on your network use:  ws://' + n.address + ':' + PORT);
  });
}
module.exports = { server, wss, rooms };
