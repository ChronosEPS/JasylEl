const state = {
  token: localStorage.getItem("jasyl_token") || "",
  user: JSON.parse(localStorage.getItem("jasyl_user") || "null"),
  regions: [],
  selectedRegionId: 1,
  trees: [],
  buildings: [],
  tool: "move",
  selectedTree: null,
  selectedBuilding: null,
  game: null,
  scene: null,
  zones: [],
  groundLayer: null,
  objectSprites: new Map(),
  eventSprites: new Map(),
  npcSprites: new Map()
};

const $ = (id) => document.getElementById(id);

async function api(path, options = {}) {
  const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
  if (state.token) headers.Authorization = `Bearer ${state.token}`;
  const response = await fetch(`/api${path}`, { ...options, headers });
  const data = await response.json().catch(() => ({}));
  if (response.status === 401) {
    logout();
    throw new Error("Сессия истекла. Войдите снова.");
  }
  if (!response.ok) throw new Error(data.message || `Ошибка ${response.status}`);
  return data;
}

function showToast(text) {
  const toast = $("toast");
  toast.textContent = text;
  toast.classList.add("show");
  setTimeout(() => toast.classList.remove("show"), 2600);
}

function setAuthMessage(text) {
  $("authMessage").textContent = text || "";
}

function setBusy(button, busy) {
  button.disabled = busy;
}

async function init() {
  wireAuth();
  wireGameUi();
  await loadRegions();
  if (state.token && state.user) {
    await enterGame();
  }
}

function wireAuth() {
  $("loginTab").onclick = () => switchAuth("login");
  $("registerTab").onclick = () => switchAuth("register");

  $("loginForm").onsubmit = async (event) => {
    event.preventDefault();
    const button = event.submitter;
    setBusy(button, true);
    setAuthMessage("");
    try {
      const result = await api("/auth/login", {
        method: "POST",
        body: JSON.stringify({
          emailOrUsername: $("loginName").value.trim(),
          password: $("loginPassword").value
        })
      });
      saveSession(result);
      await enterGame();
    } catch (error) {
      setAuthMessage(error.message);
    } finally {
      setBusy(button, false);
    }
  };

  $("registerForm").onsubmit = async (event) => {
    event.preventDefault();
    const button = event.submitter;
    setBusy(button, true);
    setAuthMessage("");
    try {
      const result = await api("/auth/register", {
        method: "POST",
        body: JSON.stringify({
          username: $("registerName").value.trim(),
          email: $("registerEmail").value.trim(),
          password: $("registerPassword").value,
          regionId: state.selectedRegionId
        })
      });
      saveSession(result);
      await enterGame();
    } catch (error) {
      setAuthMessage(error.message);
    } finally {
      setBusy(button, false);
    }
  };
}

function switchAuth(mode) {
  $("loginTab").classList.toggle("active", mode === "login");
  $("registerTab").classList.toggle("active", mode === "register");
  $("loginForm").classList.toggle("active", mode === "login");
  $("registerForm").classList.toggle("active", mode === "register");
  setAuthMessage("");
}

async function loadRegions() {
  try {
    state.regions = await api("/regions");
    state.selectedRegionId = state.regions[0]?.id || 1;
    renderRegions();
  } catch (error) {
    setAuthMessage("Не удалось загрузить регионы: " + error.message);
  }
}

function renderRegions() {
  $("regionGrid").innerHTML = state.regions.map(region => `
    <button type="button" class="region-option ${region.id === state.selectedRegionId ? "active" : ""}" data-region="${region.id}">
      <strong>${region.name}</strong>
      <small>${region.description}</small>
    </button>
  `).join("");
  document.querySelectorAll("[data-region]").forEach(button => {
    button.onclick = () => {
      state.selectedRegionId = Number(button.dataset.region);
      renderRegions();
    };
  });
}

function saveSession(result) {
  state.token = result.token;
  state.user = result.user;
  localStorage.setItem("jasyl_token", state.token);
  localStorage.setItem("jasyl_user", JSON.stringify(state.user));
}

function logout() {
  state.token = "";
  state.user = null;
  localStorage.removeItem("jasyl_token");
  localStorage.removeItem("jasyl_user");
  if (state.game) {
    state.game.destroy(true);
    state.game = null;
  }
  $("gameScreen").classList.add("hidden");
  $("authScreen").classList.remove("hidden");
}

function wireGameUi() {
  $("logoutButton").onclick = logout;
  $("profileButton").onclick = showProfile;
  $("quizButton").onclick = showQuiz;
  $("eventButton").onclick = spawnEvent;
  $("leaderboardButton").onclick = showLeaderboard;
  $("modalClose").onclick = () => $("modal").close();
  $("moveTool").onclick = () => setTool("move");
  $("treeTool").onclick = () => setTool("tree");
  $("buildTool").onclick = () => setTool("build");
}

async function enterGame() {
  $("authScreen").classList.add("hidden");
  $("gameScreen").classList.remove("hidden");
  await refreshProfile();
  await refreshCatalogs();
  startPhaser();
}

async function refreshProfile() {
  state.user = await api("/user/profile");
  localStorage.setItem("jasyl_user", JSON.stringify(state.user));
  $("playerName").textContent = state.user.username;
  $("playerRegion").textContent = state.user.region?.name || "Регион";
  $("levelBadge").textContent = state.user.level;
  $("coins").textContent = state.user.coins;
  $("water").textContent = state.user.water;
  $("energy").textContent = state.user.energy;
  $("eco").textContent = state.user.ecoPoints;
  $("rep").textContent = state.user.jasylElReputation;
}

async function refreshCatalogs() {
  [state.trees, state.buildings] = await Promise.all([
    api("/map/trees"),
    api("/map/buildings")
  ]);
  renderPalette();
}

function setTool(tool) {
  state.tool = tool;
  $("moveTool").classList.toggle("active", tool === "move");
  $("treeTool").classList.toggle("active", tool === "tree");
  $("buildTool").classList.toggle("active", tool === "build");
  renderPalette();
  $("toolHint").textContent = tool === "move"
    ? "Перетаскивайте карту мышью, масштабируйте колесом."
    : "Выберите объект и кликните по свободному месту на карте.";
}

function renderPalette() {
  const palette = $("palette");
  if (state.tool === "move") {
    palette.innerHTML = `<div class="hint">Карта большая: 3000x3000. NPC ходят сами, события можно создавать кнопкой сверху.</div>`;
    return;
  }
  const items = state.tool === "tree" ? state.trees : state.buildings;
  palette.innerHTML = items.map(item => {
    const unlocked = item.isUnlocked;
    const active = state.tool === "tree"
      ? state.selectedTree?.id === item.id
      : state.selectedBuilding?.id === item.id;
    return `
      <button class="palette-card ${item.rarity} ${active ? "active" : ""} ${unlocked ? "" : "locked"}" data-id="${item.id}" ${unlocked ? "" : "disabled"}>
        <strong>${item.name}<span class="rarity">${rarityRu(item.rarity)}</span></strong>
        <p>${item.description}</p>
        <div class="cost">
          <span>₸ ${item.costCoins}</span>
          ${item.costWater !== undefined ? `<span>💧 ${item.costWater}</span>` : ""}
          <span>⚡ ${item.costEnergy}</span>
        </div>
        ${unlocked ? "" : `<p>Откроется на уровне ${item.unlockLevel}</p>`}
      </button>`;
  }).join("");

  document.querySelectorAll(".palette-card[data-id]").forEach(button => {
    button.onclick = () => {
      const id = Number(button.dataset.id);
      if (state.tool === "tree") state.selectedTree = state.trees.find(t => t.id === id);
      if (state.tool === "build") state.selectedBuilding = state.buildings.find(b => b.id === id);
      renderPalette();
    };
  });
}

function rarityRu(rarity) {
  return ({ Common: "обычное", Rare: "редкое", Epic: "эпическое", Legendary: "легендарное" })[rarity] || rarity;
}

function startPhaser() {
  if (state.game) {
    state.game.destroy(true);
    state.objectSprites.clear();
    state.eventSprites.clear();
    state.npcSprites.clear();
  }
  state.game = new Phaser.Game({
    type: Phaser.AUTO,
    parent: "phaserContainer",
    width: $("phaserContainer").clientWidth,
    height: $("phaserContainer").clientHeight,
    backgroundColor: "#cdddcf",
    scale: { mode: Phaser.Scale.RESIZE },
    scene: { create, update }
  });
}

let dragStart = null;
let camStart = null;

async function create() {
  state.scene = this;
  const scene = this;
  scene.cameras.main.setBounds(0, 0, 3000, 3000);
  scene.cameras.main.centerOn(1500, 1500);
  scene.cameras.main.setZoom(.72);

  drawGround(scene);
  scene.input.on("pointerdown", pointer => {
    dragStart = { x: pointer.x, y: pointer.y };
    camStart = { x: scene.cameras.main.scrollX, y: scene.cameras.main.scrollY };
  });
  scene.input.on("pointermove", pointer => {
    if (!pointer.isDown || !dragStart || state.tool !== "move") return;
    const zoom = scene.cameras.main.zoom;
    scene.cameras.main.setScroll(
      camStart.x - (pointer.x - dragStart.x) / zoom,
      camStart.y - (pointer.y - dragStart.y) / zoom
    );
  });
  scene.input.on("pointerup", async pointer => {
    const moved = dragStart && (Math.abs(pointer.x - dragStart.x) > 5 || Math.abs(pointer.y - dragStart.y) > 5);
    dragStart = null;
    if (moved || state.tool === "move") return;
    const point = scene.cameras.main.getWorldPoint(pointer.x, pointer.y);
    await placeAt(point.x, point.y);
  });
  scene.input.on("wheel", (_pointer, _objects, _dx, dy) => {
    const cam = scene.cameras.main;
    cam.setZoom(Phaser.Math.Clamp(cam.zoom - dy * .001, .38, 1.45));
  });

  await refreshMap();
  setInterval(refreshMap, 12000);
}

function update() {
  for (const npc of state.npcSprites.values()) {
    const dx = npc.tx - npc.container.x;
    const dy = npc.ty - npc.container.y;
    if (Math.hypot(dx, dy) < 8) {
      npc.tx = 200 + Math.random() * 2600;
      npc.ty = 200 + Math.random() * 2600;
    } else {
      npc.container.x += dx * .006;
      npc.container.y += dy * .006;
    }
  }
}

function drawGround(scene, zones = []) {
  if (state.groundLayer) state.groundLayer.destroy();
  const g = scene.add.graphics();
  state.groundLayer = g;
  g.fillStyle(0xbfd7bd, 1);
  g.fillRect(0, 0, 3000, 3000);
  for (let x = 0; x <= 3000; x += 120) {
    g.lineStyle(1, 0xa8c4aa, .45);
    g.lineBetween(x, 0, x, 3000);
  }
  for (let y = 0; y <= 3000; y += 120) {
    g.lineStyle(1, 0xa8c4aa, .45);
    g.lineBetween(0, y, 3000, y);
  }
  for (let i = 0; i < 260; i++) {
    g.fillStyle([0x9fc592, 0xd1c28b, 0x8fb583][i % 3], .42);
    g.fillCircle(Math.random() * 3000, Math.random() * 3000, 8 + Math.random() * 24);
  }
  zones.forEach(zone => {
    const color = ({
      city: 0x7f8c8d,
      sand: 0xe4c26e,
      dry: 0xd9b67a,
      water: 0x5dade2,
      forest: 0x4caf50,
      fertile: 0x7cb342,
      steppe: 0xa4c48e
    })[zone.visual || "steppe"] || 0xa4c48e;
    const alpha = zone.canPlant ? 0.16 : 0.28;
    g.fillStyle(color, alpha);
    g.fillRoundedRect(zone.x, zone.y, zone.width, zone.height, 18);
    g.lineStyle(2, color, 0.55);
    g.strokeRoundedRect(zone.x, zone.y, zone.width, zone.height, 18);
  });
}

async function refreshMap() {
  if (!state.scene) return;
  const data = await api("/map");
  const objects = data.objects || data.Objects || [];
  const events = data.events || data.Events || [];
  state.zones = data.zones || data.Zones || [];
  drawGround(state.scene, state.zones);
  const npcs = data.npcs || data.npCs || data.NPCs || [];
  state.objectSprites.forEach(sprite => sprite.destroy());
  state.eventSprites.forEach(sprite => sprite.destroy());
  state.npcSprites.forEach(npc => npc.container.destroy());
  state.objectSprites.clear();
  state.eventSprites.clear();
  state.npcSprites.clear();
  objects.forEach(drawObject);
  events.forEach(drawEvent);
  npcs.forEach(drawNpc);
}

function drawObject(obj) {
  const scene = state.scene;
  const c = scene.add.container(obj.positionX, obj.positionY);
  if (obj.objectType === "Tree") drawTree(c, obj);
  else drawBuilding(c, obj);
  c.setSize(obj.width || 80, obj.height || 80);
  c.setInteractive();
  c.on("pointerdown", pointer => {
    pointer.event.stopPropagation();
    showObject(obj);
  });
  state.objectSprites.set(obj.id, c);
}

function drawTree(container, obj) {
  const scene = state.scene;
  const w = obj.width || 60;
  const h = obj.height || 80;
  const g = scene.add.graphics();
  const trunk = Phaser.Display.Color.HexStringToColor(obj.trunkColor || "#7b5130").color;
  const leaf = Phaser.Display.Color.HexStringToColor(obj.leafColor || "#2f8f46").color;
  g.fillStyle(trunk, 1);
  g.fillRoundedRect(-w * .08, -h * .28, w * .16, h * .34, 4);
  if (obj.growthStage === "Seed") {
    g.fillStyle(0x6b4f2a, 1);
    g.fillEllipse(0, 0, 18, 10);
  } else if (obj.growthStage === "Sapling") {
    g.lineStyle(4, trunk, 1);
    g.lineBetween(0, 0, 0, -h * .42);
    g.fillStyle(leaf, 1);
    g.fillEllipse(-10, -h * .35, 28, 18);
    g.fillEllipse(12, -h * .46, 28, 18);
  } else {
    g.fillStyle(leaf, obj.isDry ? .65 : 1);
    g.fillCircle(0, -h * .58, w * .34);
    g.fillCircle(-w * .22, -h * .44, w * .28);
    g.fillCircle(w * .22, -h * .44, w * .28);
    if (obj.treeRarity === "Legendary") {
      g.fillStyle(0xffe08a, .85);
      g.fillCircle(w * .18, -h * .65, 7);
      g.fillCircle(-w * .12, -h * .48, 6);
    }
  }
  if (obj.isOnFire) {
    const flame = scene.add.text(0, -h * .8, "🔥", { fontSize: "28px" }).setOrigin(.5);
    scene.tweens.add({ targets: flame, y: flame.y - 8, alpha: .45, yoyo: true, repeat: -1, duration: 420 });
    container.add(flame);
  }
  if (obj.isDiseased) container.add(scene.add.text(w * .35, -h * .55, "●", { fontSize: "18px", color: "#8b2f2f" }).setOrigin(.5));
  container.add(g);
  container.add(scene.add.text(0, 14, obj.treeName || "Дерево", { fontSize: "13px", color: "#17211b", backgroundColor: "rgba(255,255,255,.72)", padding: { x: 5, y: 2 } }).setOrigin(.5, 0));
}

function drawBuilding(container, obj) {
  const scene = state.scene;
  const w = obj.width || 100;
  const h = obj.height || 80;
  const g = scene.add.graphics();
  g.fillStyle(0x7c9b7f, 1);
  g.fillRoundedRect(-w / 2, -h / 2, w, h, 8);
  g.fillStyle(0xf7f2df, 1);
  g.fillRoundedRect(-w * .38, -h * .32, w * .76, h * .54, 6);
  g.fillStyle(0x5d7b65, 1);
  g.fillTriangle(-w * .46, -h * .30, 0, -h * .62, w * .46, -h * .30);
  g.fillStyle(0x2f80ed, .9);
  g.fillRect(-w * .12, -h * .05, w * .24, h * .27);
  container.add(g);
  container.add(scene.add.text(0, -h * .72, `Ур. ${obj.buildingLevel}`, { fontSize: "13px", color: "#fff", backgroundColor: "#2f8f46", padding: { x: 6, y: 2 } }).setOrigin(.5));
  container.add(scene.add.text(0, h * .34, obj.buildingName || "Постройка", { fontSize: "13px", color: "#17211b", backgroundColor: "rgba(255,255,255,.74)", padding: { x: 5, y: 2 } }).setOrigin(.5, 0));
}

function drawEvent(event) {
  const scene = state.scene;
  const icon = { Fire: "🔥", Drought: "☀️", Trash: "🗑️", Disease: "🦠" }[event.eventType] || "!";
  const text = scene.add.text(event.positionX, event.positionY, icon, { fontSize: "36px" }).setOrigin(.5).setInteractive();
  text.on("pointerdown", async pointer => {
    pointer.event.stopPropagation();
    await api(`/map/resolve-event/${event.id}`, { method: "POST" });
    showToast("Проблема решена, территория стала чище.");
    await refreshProfile();
    await refreshMap();
  });
  scene.tweens.add({ targets: text, scale: 1.15, yoyo: true, repeat: -1, duration: 620 });
  state.eventSprites.set(event.id, text);
}

function drawNpc(npc) {
  const scene = state.scene;
  const icons = { Volunteer: "👷", Forester: "🌲", Ecologist: "🔬", Firefighter: "🚒" };
  const container = scene.add.container(npc.positionX, npc.positionY);
  container.add(scene.add.text(0, 0, icons[npc.role] || "👤", { fontSize: "32px" }).setOrigin(.5));
  container.add(scene.add.text(0, 24, npc.name, { fontSize: "12px", color: "#17211b", backgroundColor: "rgba(255,255,255,.75)", padding: { x: 4, y: 2 } }).setOrigin(.5));
  state.npcSprites.set(npc.id, { container, tx: npc.targetX || 1500, ty: npc.targetY || 1500 });
}

async function placeAt(x, y) {
  try {
    if (state.tool === "tree" && state.selectedTree) {
      const check = await api(`/map/can-plant?x=${encodeURIComponent(x)}&y=${encodeURIComponent(y)}&treeTypeId=${state.selectedTree.id}`);
      if (!check.canPlant) {
        showToast(check.reason || "В этой зоне сажать нельзя.");
        return;
      }
      await api("/map/plant", { method: "POST", body: JSON.stringify({ treeTypeId: state.selectedTree.id, positionX: x, positionY: y }) });
      showToast(`Посажено: ${state.selectedTree.name} (${check.zoneType})`);
    } else if (state.tool === "build" && state.selectedBuilding) {
      await api("/map/build", { method: "POST", body: JSON.stringify({ buildingTypeId: state.selectedBuilding.id, positionX: x, positionY: y }) });
      showToast(`Построено: ${state.selectedBuilding.name}`);
    } else {
      showToast("Сначала выберите объект в панели.");
      return;
    }
    await refreshProfile();
    await refreshMap();
  } catch (error) {
    showToast(error.message);
  }
}

function showModal(title, html) {
  $("modalTitle").textContent = title;
  $("modalBody").innerHTML = html;
  $("modal").showModal();
}

function showProfile() {
  const u = state.user;
  showModal("Профиль", `
    <div class="modal-grid">
      <div class="stat-row"><b>${u.username}</b><br>${u.email}</div>
      <div class="stat-row">Регион: <b>${u.region?.name || "не выбран"}</b><br>${u.region?.description || ""}</div>
      <div class="stat-row">Уровень ${u.level}, опыт ${u.experience}/${u.experienceToNextLevel}</div>
      <div class="stat-row">Монеты ${u.coins}, вода ${u.water}, энергия ${u.energy}</div>
      <div class="stat-row">Экология ${u.ecoPoints}, репутация Жасыл Ел ${u.jasylElReputation}</div>
    </div>
  `);
}

function showObject(obj) {
  const isTree = obj.objectType === "Tree";
  showModal(isTree ? obj.treeName : obj.buildingName, `
    <div class="modal-grid">
      <div class="stat-row">Тип: <b>${isTree ? "Дерево" : "Постройка"}</b></div>
      ${isTree ? `<div class="stat-row">Редкость: ${rarityRu(obj.treeRarity)}<br>Стадия: ${stageRu(obj.growthStage)}<br>Здоровье: ${Math.round(obj.health)}%</div>` : `<div class="stat-row">Уровень: ${obj.buildingLevel}</div><button class="primary" id="upgradeObject">Улучшить</button>`}
      <div class="stat-row">Координаты: ${Math.round(obj.positionX)}, ${Math.round(obj.positionY)}</div>
    </div>
  `);
  const upgrade = $("upgradeObject");
  if (upgrade) {
    upgrade.onclick = async () => {
      try {
        await api("/map/upgrade", { method: "POST", body: JSON.stringify({ mapObjectId: obj.id }) });
        $("modal").close();
        showToast("Постройка улучшена.");
        await refreshProfile();
        await refreshMap();
      } catch (error) {
        showToast(error.message);
      }
    };
  }
}

function stageRu(stage) {
  return ({ Seed: "семя", Sapling: "саженец", Sprout: "молодой росток", Adult: "взрослое дерево" })[stage] || stage;
}

async function spawnEvent() {
  try {
    await api("/map/spawn-event", { method: "POST" });
    showToast("На карте появилось экологическое событие.");
    await refreshMap();
  } catch (error) {
    showToast(error.message);
  }
}

async function showLeaderboard() {
  const rows = await api("/user/leaderboard");
  showModal("Рейтинг", `<div class="modal-grid">${rows.map(r => `
    <div class="leader-row"><b>${r.rank}. ${r.username}</b><br>Уровень ${r.level}, экология ${r.ecoPoints}, деревья ${r.treesPlanted}, регион ${r.regionName}</div>
  `).join("") || "Пока нет игроков."}</div>`);
}

async function showQuiz() {
  const categories = await api("/quiz/categories");
  showModal("Викторина", `<div class="modal-grid">${categories.map(c => `
    <button class="quiz-option" data-category="${c.id}">
      <b>${c.name}</b><br>${c.description}<br>Прогресс: ${c.completedByUser}/${c.totalQuestions}
    </button>
  `).join("")}</div>`);
  document.querySelectorAll("[data-category]").forEach(button => {
    button.onclick = () => startQuiz(Number(button.dataset.category));
  });
}

async function startQuiz(categoryId) {
  const questions = await api(`/quiz/questions/${categoryId}`);
  if (!questions.length) {
    showToast("В этой категории пока нет вопросов.");
    return;
  }
  renderQuestion(questions, 0);
}

function renderQuestion(questions, index) {
  const q = questions[index];
  showModal("Викторина", `
    <div class="modal-grid">
      <div class="stat-row"><b>Вопрос ${index + 1}/${questions.length}</b><br>${q.questionText}</div>
      <div>${q.answerOptions.map((answer, i) => `<button class="quiz-option" data-answer="${i}">${answer}</button>`).join("")}</div>
      <div class="stat-row">Награда: ₸ ${q.rewardCoins}, опыт ${q.rewardExperience}, вода ${q.rewardWater}, энергия ${q.rewardEnergy}</div>
    </div>
  `);
  document.querySelectorAll("[data-answer]").forEach(button => {
    button.onclick = async () => {
      const answerIndex = Number(button.dataset.answer);
      const result = await api("/quiz/answer", { method: "POST", body: JSON.stringify({ questionId: q.id, answerIndex }) });
      button.classList.add(result.isCorrect ? "correct" : "wrong");
      showToast(result.isCorrect ? "Верно! Награда получена." : "Ответ неверный.");
      await refreshProfile();
      setTimeout(() => {
        if (index + 1 < questions.length) renderQuestion(questions, index + 1);
        else showQuiz();
      }, 1200);
    };
  });
}

init();
