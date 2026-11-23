/**
 * MyBUPGI v1.6.0 — Online Co-op Multiplayer, Private Groups, Voice & Text Chat
 * Uses PeerJS (WebRTC DataChannel + MediaStream) so it runs 100% over HTTPS on GitHub Pages
 * with zero backend server required.
 */
(function () {
  const ROOM_PREFIX = "mybupgi-grp-";
  const NAME_KEY = "mybupgi_player_name";
  const DEFAULT_PREFIXES = ["مقاتل", "قناص", "صقر", "نمر", "بطل", "فارس", "شبح", "أسد"];

  let unityInstance = null;
  let unityReady = false;

  let peer = null;
  let myPeerId = null;
  let myName = loadOrCreateDefaultName();
  let isHost = false;
  let roomCode = null;

  // Host holds connections to all clients; Client holds connection to host
  const connections = new Map(); // peerId -> DataConnection
  const roster = new Map();      // peerId -> { id, name, score, kills, hp, isHost, colorIndex }
  const voiceCalls = new Map();  // peerId -> MediaConnection
  const remoteAudios = new Map();// peerId -> HTMLAudioElement

  let localStream = null;
  let micEnabled = false;
  let unreadCount = 0;
  let lastLocalState = { x: 0, y: 0, z: 0, ry: 0, score: 0, kills: 0, hp: 100, firing: false };

  function loadOrCreateDefaultName() {
    try {
      const saved = localStorage.getItem(NAME_KEY);
      if (saved && saved.trim().length >= 2) return saved.trim().slice(0, 20);
    } catch (e) {}
    const prefix = DEFAULT_PREFIXES[Math.floor(Math.random() * DEFAULT_PREFIXES.length)];
    const num = Math.floor(100 + Math.random() * 900);
    const name = prefix + "-" + num;
    try { localStorage.setItem(NAME_KEY, name); } catch (e) {}
    return name;
  }

  function savePlayerName(newName) {
    const clean = (newName || "").trim().slice(0, 20);
    if (!clean) return;
    myName = clean;
    try { localStorage.setItem(NAME_KEY, myName); } catch (e) {}
    if (myPeerId && roster.has(myPeerId)) {
      roster.get(myPeerId).name = myName;
      broadcastMeta();
      renderRoster();
    }
  }

  function generateRoomCode() {
    return String(Math.floor(100000 + Math.random() * 900000));
  }

  function getInviteUrl() {
    if (!roomCode) return window.location.href.split("?")[0];
    return window.location.origin + window.location.pathname + "?room=" + encodeURIComponent(roomCode);
  }

  // ---- Unity Bridge (with lightweight Arabic reshaper for Unity legacy UI.Text nameplates) ----
  const AR_FORMS = {
    0x0621:[0xFE80,0xFE80,0xFE80,0xFE80],0x0622:[0xFE81,0xFE82,0xFE81,0xFE82],
    0x0623:[0xFE83,0xFE84,0xFE83,0xFE84],0x0624:[0xFE85,0xFE86,0xFE85,0xFE86],
    0x0625:[0xFE87,0xFE88,0xFE87,0xFE88],0x0626:[0xFE89,0xFE8A,0xFE8B,0xFE8C],
    0x0627:[0xFE8D,0xFE8E,0xFE8D,0xFE8E],0x0628:[0xFE8F,0xFE90,0xFE91,0xFE92],
    0x0629:[0xFE93,0xFE94,0xFE93,0xFE94],0x062A:[0xFE95,0xFE96,0xFE97,0xFE98],
    0x062B:[0xFE99,0xFE9A,0xFE9B,0xFE9C],0x062C:[0xFE9D,0xFE9E,0xFE9F,0xFEA0],
    0x062D:[0xFEA1,0xFEA2,0xFEA3,0xFEA4],0x062E:[0xFEA5,0xFEA6,0xFEA7,0xFEA8],
    0x062F:[0xFEA9,0xFEAA,0xFEA9,0xFEAA],0x0630:[0xFEAB,0xFEAC,0xFEAB,0xFEAC],
    0x0631:[0xFEAD,0xFEAE,0xFEAD,0xFEAE],0x0632:[0xFEAF,0xFEB0,0xFEAF,0xFEB0],
    0x0633:[0xFEB1,0xFEB2,0xFEB3,0xFEB4],0x0634:[0xFEB5,0xFEB6,0xFEB7,0xFEB8],
    0x0635:[0xFEB9,0xFEBA,0xFEBB,0xFEBC],0x0636:[0xFEBD,0xFEBE,0xFEBF,0xFEC0],
    0x0637:[0xFEC1,0xFEC2,0xFEC3,0xFEC4],0x0638:[0xFEC5,0xFEC6,0xFEC7,0xFEC8],
    0x0639:[0xFEC9,0xFECA,0xFECB,0xFECC],0x063A:[0xFECD,0xFECE,0xFECF,0xFED0],
    0x0641:[0xFED1,0xFED2,0xFED3,0xFED4],0x0642:[0xFED5,0xFED6,0xFED7,0xFED8],
    0x0643:[0xFED9,0xFEDA,0xFEDB,0xFEDC],0x0644:[0xFEDD,0xFEDE,0xFEDF,0xFEE0],
    0x0645:[0xFEE1,0xFEE2,0xFEE3,0xFEE4],0x0646:[0xFEE5,0xFEE6,0xFEE7,0xFEE8],
    0x0647:[0xFEE9,0xFEEA,0xFEEB,0xFEEC],0x0648:[0xFEED,0xFEEE,0xFEED,0xFEEE],
    0x0649:[0xFEEF,0xFEF0,0xFEEF,0xFEF0],0x064A:[0xFEF1,0xFEF2,0xFEF3,0xFEF4]
  };
  const NON_LEFT = new Set([0x0621,0x0622,0x0623,0x0624,0x0625,0x0627,0x0629,0x062F,0x0630,0x0631,0x0632,0x0648,0x0649]);

  function reshapeForUnity(str) {
    if (!str || !/[\u0600-\u06FF]/.test(str)) return str || "";
    const parts = str.split(" ").reverse().map(tok => {
      if (!/[\u0600-\u06FF]/.test(tok)) return tok;
      let out = "";
      for (let i = 0; i < tok.length; i++) {
        const c = tok.charCodeAt(i);
        if (!AR_FORMS[c]) { out += tok[i]; continue; }
        const prev = i > 0 ? tok.charCodeAt(i - 1) : 0;
        const next = i < tok.length - 1 ? tok.charCodeAt(i + 1) : 0;
        const pConn = !!AR_FORMS[prev] && !NON_LEFT.has(prev);
        const nConn = !!AR_FORMS[next];
        const canL = !NON_LEFT.has(c);
        const f = AR_FORMS[c];
        const g = (pConn && nConn && canL) ? f[3] : (pConn ? f[1] : ((nConn && canL) ? f[2] : f[0]));
        out += String.fromCharCode(g);
      }
      return out.split("").reverse().join("");
    });
    return parts.join(" ");
  }

  function sendToUnity(method, payload) {
    if (!unityInstance || !unityReady) return;
    try {
      let data = payload;
      if (data && typeof data === "object" && typeof data.name === "string") {
        data = Object.assign({}, data, { name: reshapeForUnity(data.name) });
      }
      unityInstance.SendMessage("MultiplayerManager", method, typeof data === "string" ? data : JSON.stringify(data));
    } catch (e) {}
  }

  window.MyBUPGI_MP = {
    attachUnity: function (inst) {
      unityInstance = inst;
    },
    onUnityReady: function () {
      unityReady = true;
      // Sync existing roster into Unity if already in a room
      roster.forEach((p, id) => {
        if (id !== myPeerId) sendToUnity("OnPeerJoined", { id: p.id, name: p.name, colorIndex: p.colorIndex || 0 });
      });
    },
    onUnityLocalState: function (jsonStr) {
      try {
        const s = JSON.parse(jsonStr);
        lastLocalState = s;
        if (!roomCode || !myPeerId) return;
        const me = roster.get(myPeerId);
        if (me) {
          const scoreChanged = me.score !== s.score || me.kills !== s.kills;
          me.score = s.score;
          me.kills = s.kills;
          me.hp = s.hp;
          if (scoreChanged) renderRoster();
        }
        const packet = {
          type: "state",
          id: myPeerId,
          name: myName,
          x: s.x, y: s.y, z: s.z, ry: s.ry,
          score: s.score, kills: s.kills, hp: s.hp, firing: !!s.firing
        };
        if (isHost) {
          broadcastToClients(packet, null);
        } else {
          const hostConn = connections.get(ROOM_PREFIX + roomCode);
          if (hostConn && hostConn.open) hostConn.send(packet);
        }
      } catch (e) {}
    }
  };

  // ---- PeerJS Networking ----
  function createPeerInstance(customId) {
    if (typeof Peer === "undefined") {
      setStatus("تعذر تحميل مكتبة الاتصال (PeerJS). تأكد من اتصالك بالإنترنت.", true);
      return null;
    }
    const opts = {
      debug: 1,
      config: {
        iceServers: [
          { urls: "stun:stun.l.google.com:19302" },
          { urls: "stun:stun1.l.google.com:19302" }
        ]
      }
    };
    return customId ? new Peer(customId, opts) : new Peer(opts);
  }

  function leaveRoom(silent) {
    stopMic(true);
    voiceCalls.forEach(c => { try { c.close(); } catch (e) {} });
    voiceCalls.clear();
    remoteAudios.forEach(a => { try { a.remove(); } catch (e) {} });
    remoteAudios.clear();

    connections.forEach(c => { try { c.close(); } catch (e) {} });
    connections.clear();
    roster.clear();

    if (peer) {
      try { peer.destroy(); } catch (e) {}
      peer = null;
    }

    myPeerId = null;
    isHost = false;
    roomCode = null;
    sendToUnity("OnClearAllPeers", "");
    updateRoomUI();
    renderRoster();
    if (!silent) appendSystemChat("غادرت المجموعة.");
  }

  function createRoom() {
    leaveRoom(true);
    const code = generateRoomCode();
    const hostPeerId = ROOM_PREFIX + code;
    setStatus("جاري إنشاء المجموعة...", false);

    peer = createPeerInstance(hostPeerId);
    if (!peer) return;

    peer.on("open", (id) => {
      myPeerId = id;
      isHost = true;
      roomCode = code;
      roster.set(id, { id, name: myName, score: lastLocalState.score, kills: lastLocalState.kills, hp: 100, isHost: true, colorIndex: 0 });
      setStatus("تم إنشاء المجموعة! أرسل الكود لأصدقائك.", false);
      appendSystemChat("تم إنشاء المجموعة الخاصة بكود: " + code);
      updateRoomUI();
      renderRoster();
    });

    peer.on("connection", (conn) => setupHostConnection(conn));
    peer.on("call", (call) => handleIncomingVoiceCall(call));
    peer.on("error", (err) => {
      if (err && err.type === "unavailable-id") {
        // Rare collision, retry with new code
        createRoom();
      } else {
        setStatus("خطأ في الاتصال: " + (err ? err.type : "unknown"), true);
      }
    });
  }

  function joinRoom(codeInput) {
    const clean = (codeInput || "").replace(/[^0-9A-Za-z]/g, "").trim();
    if (clean.length < 4) {
      setStatus("أدخل كود مجموعة صحيح (6 أرقام).", true);
      return;
    }
    leaveRoom(true);
    roomCode = clean;
    isHost = false;
    setStatus("جاري الاتصال بالمجموعة " + clean + "...", false);

    peer = createPeerInstance(null);
    if (!peer) return;

    peer.on("open", (id) => {
      myPeerId = id;
      const hostId = ROOM_PREFIX + clean;
      const conn = peer.connect(hostId, { reliable: false, metadata: { name: myName } });
      connections.set(hostId, conn);

      conn.on("open", () => {
        roster.set(id, { id, name: myName, score: lastLocalState.score, kills: lastLocalState.kills, hp: 100, isHost: false, colorIndex: 1 });
        conn.send({ type: "hello", id: myPeerId, name: myName });
        setStatus("تم الانضمام للمجموعة " + clean + " بنجاح!", false);
        appendSystemChat("انضممت إلى المجموعة " + clean);
        updateRoomUI();
        renderRoster();
        if (micEnabled && localStream) callPeerVoice(hostId);
      });

      conn.on("data", (msg) => handleClientData(msg));
      conn.on("close", () => {
        setStatus("انقطع الاتصال بصاحب المجموعة.", true);
        appendSystemChat("انتهت جلسة المجموعة.");
        leaveRoom(true);
      });
      conn.on("error", () => {
        setStatus("تعذر الانضمام — تأكد من صحة الكود وأن صاحب المجموعة متصل.", true);
      });
    });

    peer.on("call", (call) => handleIncomingVoiceCall(call));
    peer.on("error", (err) => {
      setStatus("تعذر العثور على المجموعة " + clean + ".", true);
    });
  }

  function setupHostConnection(conn) {
    if (connections.size >= 7) {
      conn.on("open", () => { conn.send({ type: "full" }); setTimeout(() => conn.close(), 200); });
      return;
    }

    conn.on("open", () => {
      connections.set(conn.peer, conn);
    });

    conn.on("data", (msg) => {
      if (!msg || typeof msg !== "object") return;
      if (msg.type === "hello") {
        const pName = (msg.name || "لاعب").trim().slice(0, 20);
        const colorIndex = roster.size;
        roster.set(conn.peer, { id: conn.peer, name: pName, score: 0, kills: 0, hp: 100, isHost: false, colorIndex });
        sendToUnity("OnPeerJoined", { id: conn.peer, name: pName, colorIndex });
        broadcastRoster();
        const joinText = "انضم " + pName + " إلى المجموعة!";
        appendSystemChat(joinText);
        broadcastToClients({ type: "sys", text: joinText }, null);
        renderRoster();
        if (micEnabled && localStream) callPeerVoice(conn.peer);
      } else if (msg.type === "state") {
        const entry = roster.get(conn.peer);
        if (entry) {
          entry.name = msg.name || entry.name;
          entry.score = msg.score || 0;
          entry.kills = msg.kills || 0;
          entry.hp = msg.hp || 100;
          renderRoster();
        }
        sendToUnity("OnPeerState", msg);
        broadcastToClients(msg, conn.peer);
      } else if (msg.type === "chat") {
        const chatMsg = { type: "chat", id: conn.peer, name: msg.name || "لاعب", text: (msg.text || "").slice(0, 240) };
        appendChat(chatMsg.name, chatMsg.text, false);
        broadcastToClients(chatMsg, conn.peer);
      } else if (msg.type === "rename") {
        const entry = roster.get(conn.peer);
        if (entry && msg.name) {
          entry.name = msg.name.slice(0, 20);
          broadcastRoster();
          renderRoster();
        }
      }
    });

    conn.on("close", () => {
      const left = roster.get(conn.peer);
      connections.delete(conn.peer);
      roster.delete(conn.peer);
      closeVoiceForPeer(conn.peer);
      sendToUnity("OnPeerLeft", conn.peer);
      broadcastRoster();
      renderRoster();
      if (left) {
        const txt = "غادر " + left.name + " المجموعة.";
        appendSystemChat(txt);
        broadcastToClients({ type: "sys", text: txt }, null);
      }
    });
  }

  function handleClientData(msg) {
    if (!msg || typeof msg !== "object") return;
    if (msg.type === "roster" && Array.isArray(msg.players)) {
      const incomingIds = new Set(msg.players.map(p => p.id));
      roster.forEach((_, id) => {
        if (!incomingIds.has(id) && id !== myPeerId) {
          roster.delete(id);
          sendToUnity("OnPeerLeft", id);
        }
      });
      msg.players.forEach(p => {
        const isNew = !roster.has(p.id);
        roster.set(p.id, p);
        if (isNew && p.id !== myPeerId) {
          sendToUnity("OnPeerJoined", { id: p.id, name: p.name, colorIndex: p.colorIndex || 1 });
          if (micEnabled && localStream) callPeerVoice(p.id);
        }
      });
      renderRoster();
    } else if (msg.type === "state") {
      if (msg.id === myPeerId) return;
      const entry = roster.get(msg.id);
      if (entry) {
        entry.score = msg.score || 0;
        entry.kills = msg.kills || 0;
        entry.hp = msg.hp || 100;
        renderRoster();
      }
      sendToUnity("OnPeerState", msg);
    } else if (msg.type === "chat") {
      appendChat(msg.name || "لاعب", msg.text || "", msg.id === myPeerId);
    } else if (msg.type === "sys") {
      appendSystemChat(msg.text || "");
    } else if (msg.type === "full") {
      setStatus("المجموعة ممتلئة حالياً (الحد الأقصى 8 لاعبين).", true);
      leaveRoom(true);
    }
  }

  function broadcastToClients(packet, excludePeerId) {
    connections.forEach((conn, id) => {
      if (id !== excludePeerId && conn.open) {
        try { conn.send(packet); } catch (e) {}
      }
    });
  }

  function broadcastRoster() {
    if (!isHost) return;
    const players = Array.from(roster.values());
    broadcastToClients({ type: "roster", players }, null);
  }

  function broadcastMeta() {
    if (!roomCode) return;
    if (isHost) {
      broadcastRoster();
    } else {
      const hostConn = connections.get(ROOM_PREFIX + roomCode);
      if (hostConn && hostConn.open) hostConn.send({ type: "rename", name: myName });
    }
  }

  // ---- Chat ----
  function sendChatMessage(rawText) {
    const text = (rawText || "").trim().slice(0, 240);
    if (!text) return;
    if (!roomCode) {
      appendSystemChat("أنشئ مجموعة أو انضم بكود أولاً للتحدث مع أصدقائك.");
      return;
    }
    appendChat(myName, text, true);
    const packet = { type: "chat", id: myPeerId, name: myName, text };
    if (isHost) {
      broadcastToClients(packet, null);
    } else {
      const hostConn = connections.get(ROOM_PREFIX + roomCode);
      if (hostConn && hostConn.open) hostConn.send(packet);
    }
  }

  // ---- Voice Chat (WebRTC Audio) ----
  async function toggleMic() {
    if (!roomCode) {
      setStatus("انضم إلى مجموعة أو أنشئ واحدة أولاً لتشغيل المايكروفون.", true);
      openPanel("group");
      return;
    }
    if (micEnabled) {
      stopMic(false);
      return;
    }
    try {
      localStream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
        video: false
      });
      micEnabled = true;
      updateMicUI();
      appendSystemChat("🎤 تم تشغيل المايكروفون — يمكنك التحدث مع مجموعتك الآن.");
      // Call every other peer currently in the room
      roster.forEach((_, id) => {
        if (id !== myPeerId) callPeerVoice(id);
      });
    } catch (err) {
      setStatus("تعذر الوصول للمايكروفون — تأكد من السماح للمتصفح باستخدام الصوت.", true);
    }
  }

  function stopMic(silent) {
    if (localStream) {
      localStream.getTracks().forEach(t => t.stop());
      localStream = null;
    }
    micEnabled = false;
    updateMicUI();
    if (!silent) appendSystemChat("🔇 تم كتم المايكروفون.");
  }

  function callPeerVoice(targetPeerId) {
    if (!peer || !localStream || !targetPeerId || targetPeerId === myPeerId) return;
    try {
      const call = peer.call(targetPeerId, localStream);
      if (!call) return;
      voiceCalls.set(targetPeerId, call);
      call.on("stream", (remoteStream) => attachRemoteAudio(targetPeerId, remoteStream));
      call.on("close", () => closeVoiceForPeer(targetPeerId));
    } catch (e) {}
  }

  function handleIncomingVoiceCall(call) {
    try {
      call.answer(localStream || undefined);
      voiceCalls.set(call.peer, call);
      call.on("stream", (remoteStream) => attachRemoteAudio(call.peer, remoteStream));
      call.on("close", () => closeVoiceForPeer(call.peer));
    } catch (e) {}
  }

  function attachRemoteAudio(peerId, stream) {
    let audio = remoteAudios.get(peerId);
    if (!audio) {
      audio = document.createElement("audio");
      audio.autoplay = true;
      audio.playsInline = true;
      audio.style.display = "none";
      document.body.appendChild(audio);
      remoteAudios.set(peerId, audio);
    }
    audio.srcObject = stream;
    audio.play().catch(() => {});
  }

  function closeVoiceForPeer(peerId) {
    const c = voiceCalls.get(peerId);
    if (c) { try { c.close(); } catch (e) {} voiceCalls.delete(peerId); }
    const a = remoteAudios.get(peerId);
    if (a) { try { a.remove(); } catch (e) {} remoteAudios.delete(peerId); }
  }

  // ---- UI Wiring ----
  function $(id) { return document.getElementById(id); }

  function setStatus(msg, isError) {
    const el = $("mp-status");
    if (!el) return;
    el.textContent = msg;
    el.style.color = isError ? "#ff6b6b" : "#4ade80";
  }

  function updateMicUI() {
    const btn = $("mp-mic-btn");
    if (!btn) return;
    btn.textContent = micEnabled ? "🎤 المايك يعمل" : "🔇 المايك";
    btn.classList.toggle("mp-btn-live", micEnabled);
  }

  function updateRoomUI() {
    const activeBox = $("mp-room-active");
    const setupBox = $("mp-room-setup");
    const codeLabel = $("mp-room-code-display");
    const grpBtn = $("mp-group-btn");

    if (roomCode) {
      if (activeBox) activeBox.style.display = "block";
      if (setupBox) setupBox.style.display = "none";
      if (codeLabel) codeLabel.textContent = roomCode;
      if (grpBtn) grpBtn.textContent = "👥 مجموعة: " + roomCode;
    } else {
      if (activeBox) activeBox.style.display = "none";
      if (setupBox) setupBox.style.display = "block";
      if (grpBtn) grpBtn.textContent = "👥 المجموعات";
    }
  }

  function renderRoster() {
    const list = $("mp-roster-list");
    if (!list) return;
    list.innerHTML = "";
    const players = Array.from(roster.values()).sort((a, b) => (b.score || 0) - (a.score || 0));
    if (players.length === 0) {
      list.innerHTML = '<div class="mp-empty">لست في مجموعة بعد.</div>';
      return;
    }
    players.forEach((p, idx) => {
      const row = document.createElement("div");
      row.className = "mp-roster-row" + (p.id === myPeerId ? " mp-me" : "");
      const badge = p.isHost ? "👑 " : "";
      row.innerHTML =
        '<span class="mp-r-name">' + (idx + 1) + ". " + badge + escapeHtml(p.name) + '</span>' +
        '<span class="mp-r-stats">' + (p.score || 0) + ' نقطة • ' + (p.kills || 0) + ' قتل</span>';
      list.appendChild(row);
    });
  }

  function appendChat(sender, text, isMe) {
    const box = $("mp-chat-messages");
    if (!box) return;
    const div = document.createElement("div");
    div.className = "mp-msg" + (isMe ? " mp-msg-me" : "");
    div.innerHTML = '<span class="mp-msg-sender">' + escapeHtml(sender) + ':</span> ' + escapeHtml(text);
    box.appendChild(div);
    box.scrollTop = box.scrollHeight;

    const chatDrawer = $("mp-chat-drawer");
    if (chatDrawer && !chatDrawer.classList.contains("open") && !isMe) {
      unreadCount++;
      updateUnreadBadge();
    }
  }

  function appendSystemChat(text) {
    const box = $("mp-chat-messages");
    if (!box) return;
    const div = document.createElement("div");
    div.className = "mp-msg-sys";
    div.textContent = text;
    box.appendChild(div);
    box.scrollTop = box.scrollHeight;
  }

  function updateUnreadBadge() {
    const badge = $("mp-chat-badge");
    if (!badge) return;
    if (unreadCount > 0) {
      badge.textContent = unreadCount > 9 ? "9+" : String(unreadCount);
      badge.style.display = "inline-flex";
    } else {
      badge.style.display = "none";
    }
  }

  function openPanel(which) {
    const g = $("mp-group-drawer");
    const c = $("mp-chat-drawer");
    if (which === "group") {
      const willOpen = !g.classList.contains("open");
      g.classList.toggle("open", willOpen);
      if (willOpen && c) c.classList.remove("open");
    } else if (which === "chat") {
      const willOpen = !c.classList.contains("open");
      c.classList.toggle("open", willOpen);
      if (willOpen) {
        if (g) g.classList.remove("open");
        unreadCount = 0;
        updateUnreadBadge();
        const inp = $("mp-chat-input");
        if (inp) setTimeout(() => inp.focus(), 50);
      }
    }
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  }

  function stopUnityKeyCapture(inputEl) {
    if (!inputEl) return;
    ["keydown", "keyup", "keypress"].forEach(ev => {
      inputEl.addEventListener(ev, (e) => { e.stopPropagation(); });
    });
  }

  function initUI() {
    const nameInput = $("mp-name-input");
    if (nameInput) {
      nameInput.value = myName;
      stopUnityKeyCapture(nameInput);
      nameInput.addEventListener("change", () => savePlayerName(nameInput.value));
    }

    const codeInput = $("mp-code-input");
    stopUnityKeyCapture(codeInput);

    const chatInput = $("mp-chat-input");
    stopUnityKeyCapture(chatInput);
    if (chatInput) {
      chatInput.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
          e.preventDefault();
          sendChatMessage(chatInput.value);
          chatInput.value = "";
        }
      });
    }

    const chatSend = $("mp-chat-send");
    if (chatSend) {
      chatSend.onclick = () => {
        if (!chatInput) return;
        sendChatMessage(chatInput.value);
        chatInput.value = "";
        chatInput.focus();
      };
    }

    if ($("mp-group-btn")) $("mp-group-btn").onclick = () => openPanel("group");
    if ($("mp-chat-btn")) $("mp-chat-btn").onclick = () => openPanel("chat");
    if ($("mp-mic-btn")) $("mp-mic-btn").onclick = () => toggleMic();
    if ($("mp-close-group")) $("mp-close-group").onclick = () => $("mp-group-drawer").classList.remove("open");
    if ($("mp-close-chat")) $("mp-close-chat").onclick = () => $("mp-chat-drawer").classList.remove("open");

    if ($("mp-create-btn")) $("mp-create-btn").onclick = () => {
      if (nameInput) savePlayerName(nameInput.value);
      createRoom();
    };
    if ($("mp-join-btn")) $("mp-join-btn").onclick = () => {
      if (nameInput) savePlayerName(nameInput.value);
      joinRoom(codeInput ? codeInput.value : "");
    };
    if ($("mp-leave-btn")) $("mp-leave-btn").onclick = () => leaveRoom(false);

    if ($("mp-copy-code-btn")) $("mp-copy-code-btn").onclick = () => {
      if (!roomCode) return;
      navigator.clipboard.writeText(roomCode).then(() => setStatus("تم نسخ كود المجموعة: " + roomCode, false));
    };
    if ($("mp-copy-link-btn")) $("mp-copy-link-btn").onclick = () => {
      const url = getInviteUrl();
      navigator.clipboard.writeText(url).then(() => setStatus("تم نسخ رابط الدعوة المباشر!", false));
    };
    if ($("mp-wa-invite-btn")) $("mp-wa-invite-btn").onclick = () => {
      const url = getInviteUrl();
      const msg = "🎮 انضم إلى مجموعتي الخاصة في لعبة MyBUPGI!\n🔑 كود المجموعة: " + roomCode + "\n🔗 رابط الدخول المباشر:\n" + url;
      window.open("https://wa.me/?text=" + encodeURIComponent(msg), "_blank");
    };

    updateRoomUI();
    updateMicUI();
    renderRoster();

    // Auto-join if opened with ?room=XXXXXX invite link
    const params = new URLSearchParams(window.location.search);
    const inviteRoom = params.get("room");
    if (inviteRoom && inviteRoom.trim().length >= 4) {
      if (codeInput) codeInput.value = inviteRoom.trim();
      $("mp-group-drawer").classList.add("open");
      setTimeout(() => joinRoom(inviteRoom.trim()), 600);
    }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initUI);
  } else {
    initUI();
  }
})();
