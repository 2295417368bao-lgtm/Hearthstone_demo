// ═══════════════════════════════════════════
// 炉石传说 Web UI — JavaScript Client
// ═══════════════════════════════════════════

// ── State ──
let gameState = null;
let selectedCard = null;    // { index, el, data }
let attackingEntity = null; // { entityId, el }
let targetMode = null;      // 'play' | 'attack' | 'heropower'
let connection = null;
let mulliganSelected = new Set(); // indices of cards selected for mulligan
let cardDataCache = {};     // cardId → { name, description, cost, atk, health, type, rarity, ... }
let gameMode = 'ai';        // 'ai' | 'pvp'

// ── Deck Builder State ──
let dbAllCards = [];         // all available cards from backend
let dbFilteredCards = [];    // after filter
let dbCurrentPage = 0;
let dbCurrentTab = 'class';  // 'class' | 'neutral'
let dbCostFilter = 'all';    // 'all' | '0'-'7'
let dbSearchText = '';
let dbDeck = [];             // array of { cardId, name, cost, rarity, ... }
let dbPlayerClass = 'Mage';

// ── Card name lookup (hardcoded for classic cards) ──
const CARD_NAMES = {
  // Mage
  CS2_029: "火球术", CS2_024: "寒冰箭", CS2_023: "奥术智慧",
  CS2_032: "烈焰风暴", CS2_025: "奥术飞弹", CS2_026: "冰霜新星",
  // Hunter
  DS1_185: "奥术射击", EX1_539: "杀戮命令", DS1_183: "多重射击",
  EX1_538: "关门放狗", DS1_175: "驯服的狼", DS1_178: "长鬃草原狮",
  // Warrior
  CS2_108: "斩杀", EX1_400: "旋风斩", EX1_606: "盾牌格挡",
  CS2_105: "英勇打击", CS2_106: "战斧",
  // Neutral
  CS2_168: "淡水鳄", CS2_172: "血沼迅猛龙", CS2_182: "冰风雪人",
  CS2_200: "暴风城骑士", CS2_121: "暴风城勇士", CS2_186: "战争古树",
  CS2_187: "海巨人", EX1_593: "夜刃豹", CS2_189: "精灵弓箭手",
  CS2_179: "银月守卫",
  // Heroes
  HERO_08: "🔮", HERO_05: "🏹", HERO_01: "⚔️",
  HERO_09: "✝️", HERO_06: "🌿", HERO_07: "🔥",
  HERO_04: "🛡️", HERO_03: "🗡️", HERO_02: "⚡",
};

function getCardName(cardId, backendName) {
  if (backendName) return backendName;
  // CARD_NAMES has manually curated display names (emoji for heroes, etc.)
  if (CARD_NAMES[cardId]) return CARD_NAMES[cardId];
  if (cardDataCache[cardId]) return cardDataCache[cardId].name;
  return cardId || "???";
}

function getCardData(cardId) {
  return cardDataCache[cardId] || null;
}

function getPlayerSpellPower() {
  if (!gameState) return 0;
  const p = gameState.player || gameState.Player;
  return p?.spellPower ?? p?.SpellPower ?? 0;
}

function applySpellDamage(desc, spellPower) {
  if (!desc || spellPower <= 0) return desc || '';
  return desc.replace(/\$(\d+)/g, (_, n) => {
    const base = parseInt(n);
    const boosted = base + spellPower;
    return `*${boosted}*`;
  });
}

function log(msg) {
  console.log(`[HS] ${msg}`);
  addLog(msg);
}

// ── Collection Screen ──

const HERO_ICONS = { Mage: '🔮', Hunter: '🏹', Warrior: '⚔️', Warlock: '🔥', Paladin: '🛡️', Druid: '🌿', Rogue: '🗡️', Priest: '✝️', Shaman: '🌊' };
const ALL_CLASSES = ['Mage', 'Hunter', 'Warrior', 'Warlock', 'Paladin', 'Druid', 'Rogue', 'Priest', 'Shaman'];
const CLASS_NAMES_ZH = { Mage: '法师', Hunter: '猎人', Warrior: '战士', Warlock: '术士', Paladin: '骑士', Druid: '德鲁伊', Rogue: '盗贼', Priest: '牧师', Shaman: '萨满' };

let dbMode = 'browse';     // 'browse' | 'edit'
let dbBrowseClass = 'Mage';
let dbBrowseNeutral = false; // true when viewing neutral tab in browse mode
let savedDecks = JSON.parse(localStorage.getItem('hs_decks') || '[]');
let hubReady = false;

// ── Screen Navigation ──

function showScreen(id) {
  document.querySelectorAll('.screen').forEach(s => s.classList.remove('active'));
  document.getElementById(id).classList.add('active');
}

async function ensureConnection() {
  if (hubReady) return;
  connection = new signalR.HubConnectionBuilder()
    .withUrl('/gamehub')
    .configureLogging(signalR.LogLevel.Information)
    .build();

  connection.on('FullState', (state) => {
    log('收到 FullState');
    gameState = state;
    buildEntityNameMap(state);

    // If we're not on game screen yet (PvP creator waiting), switch to it
    const gameScreen = document.getElementById('game-screen');
    if (!gameScreen.classList.contains('active')) {
      showScreen('game-screen');
    }

    const mulState = state.mulliganState || state.MulliganState || 'done';
    if (mulState === 'waiting') {
      renderMulligan(state);
    } else {
      document.getElementById('mulligan-overlay').classList.add('hidden');
      renderGameState();
    }
  });
  connection.on('Actions', (actions) => {
    log(`收到 ${actions.length} 个 Actions`);
    processActions(actions);
  });
  connection.on('Error', (msg) => log(`❌ 错误: ${msg}`));
  connection.on('RoomCreated', (code) => {
    log(`房间创建成功: ${code}`);
    // Show room code in status bar + show deck selection
    const statusBar = document.getElementById('pvp-status-bar');
    statusBar.classList.remove('hidden');
    document.getElementById('pvp-room-code').textContent = code;
    document.getElementById('pvp-opponent-status').textContent = '等待对手加入...';
    document.getElementById('pvp-opponent-status').classList.remove('ready');
    document.getElementById('pvp-room-ui').classList.add('hidden');
    document.getElementById('deck-subtitle').classList.remove('hidden');
    document.getElementById('deck-subtitle').textContent = '选择卡组开始对战';
    renderPlayDeckGrid();
  });
  connection.on('RoomJoined', (code) => {
    log(`已加入房间: ${code}`);
    // Show room code bar + deck selection
    document.getElementById('pvp-status-bar').classList.remove('hidden');
    document.getElementById('pvp-room-code').textContent = code;
    document.getElementById('pvp-opponent-status').textContent = '已连接';
    document.getElementById('pvp-opponent-status').classList.add('ready');
    document.getElementById('pvp-room-ui').classList.add('hidden');
    document.getElementById('deck-subtitle').classList.remove('hidden');
    document.getElementById('deck-subtitle').textContent = '选择卡组开始对战';
    renderPlayDeckGrid();
  });
  connection.on('OpponentJoined', () => {
    log('对手已加入房间');
    document.getElementById('pvp-opponent-status').textContent = '对手已加入 ✅';
    document.getElementById('pvp-opponent-status').classList.add('ready');
  });
  connection.on('DeckSubmitted', () => {
    log('卡组已提交');
  });
  connection.on('OpponentReady', () => {
    log('对手已准备就绪');
    document.getElementById('pvp-opponent-status').textContent = '对手已准备 ✅';
    document.getElementById('pvp-opponent-status').classList.add('ready');
  });
  connection.onclose((err) => log(`连接断开: ${err || '正常'}`));

  await connection.start();
  hubReady = true;
  log('连接成功');

  // Preload all card data via HTTP (too large for SignalR)
  try {
    const resp = await fetch('/api/cards');
    const allCards = await resp.json();
    for (const c of allCards) {
      const id = c.cardId || c.CardId;
      if (id) cardDataCache[id] = c;
    }
    log(`📖 已加载 ${Object.keys(cardDataCache).length} 张卡牌数据`);
  } catch (err) {
    log(`⚠️ 预加载卡牌数据失败: ${err.message}`);
  }
}

// ── Main Menu ──

document.getElementById('menu-play-btn').addEventListener('click', () => {
  showScreen('play-screen');
  renderPlayScreen();
});

document.getElementById('menu-collection-btn').addEventListener('click', async () => {
  showScreen('deckbuilder-screen');
  try {
    await ensureConnection();
    dbAllCards = await connection.invoke('GetAvailableCards', dbBrowseClass);
    dbRenderTabs();
    dbApplyFilters();
    dbRenderSavedDecks();
  } catch (err) { log(`❌ ${err.message}`); }
});

// Back buttons
document.getElementById('play-back-btn').addEventListener('click', () => {
  // Reset play screen state
  document.getElementById('mode-selection').classList.remove('hidden');
  document.getElementById('pvp-room-ui').classList.add('hidden');
  document.getElementById('pvp-status-bar').classList.add('hidden');
  document.getElementById('pvp-waiting').classList.add('hidden');
  document.getElementById('play-deck-grid').classList.add('hidden');
  document.getElementById('deck-subtitle')?.classList.add('hidden');
  showScreen('menu-screen');
});
document.getElementById('db-back-menu-btn').addEventListener('click', () => {
  // If editing, go back to browse first
  if (dbMode === 'edit') {
    dbMode = 'browse';
    document.getElementById('db-decks-panel').classList.remove('hidden');
    document.getElementById('db-edit-panel').classList.add('hidden');
  }
  showScreen('menu-screen');
});

// ── Mode Selection ──

document.getElementById('mode-ai-btn').addEventListener('click', () => {
  gameMode = 'ai';
  document.getElementById('mode-selection').classList.add('hidden');
  document.getElementById('deck-subtitle').classList.remove('hidden');
  renderPlayDeckGrid();
});

document.getElementById('mode-pvp-btn').addEventListener('click', () => {
  gameMode = 'pvp';
  document.getElementById('mode-selection').classList.add('hidden');
  document.getElementById('pvp-room-ui').classList.remove('hidden');
});

document.getElementById('pvp-back-btn').addEventListener('click', () => {
  document.getElementById('pvp-room-ui').classList.add('hidden');
  document.getElementById('mode-selection').classList.remove('hidden');
});

// PvP: Create Room → call backend, then show deck selection (handled by RoomCreated event)
document.getElementById('pvp-create-btn').addEventListener('click', async () => {
  gameMode = 'pvp';
  try {
    await ensureConnection();
    await connection.invoke('CreateRoom');
  } catch (err) { log(`❌ 创建房间失败: ${err.message}`); }
});

// PvP: Join Room → call backend, then show deck selection (handled by RoomJoined event)
document.getElementById('pvp-join-btn').addEventListener('click', async () => {
  const code = document.getElementById('pvp-room-input').value.trim();
  if (code.length !== 4) {
    alert('请输入 4 位房间号');
    return;
  }
  gameMode = 'pvp';
  try {
    await ensureConnection();
    await connection.invoke('JoinRoom', code);
  } catch (err) { log(`❌ 加入房间失败: ${err.message}`); }
});

// ── Play Screen ──

let playSelectedDeckIdx = -1;

function renderPlayScreen() {
  // Reset to mode selection state
  document.getElementById('mode-selection').classList.remove('hidden');
  document.getElementById('pvp-room-ui').classList.add('hidden');
  document.getElementById('pvp-status-bar').classList.add('hidden');
  document.getElementById('pvp-waiting').classList.add('hidden');
  document.getElementById('play-deck-grid').classList.add('hidden');
  document.getElementById('deck-subtitle')?.classList.add('hidden');
  document.getElementById('play-no-decks').classList.add('hidden');
  document.getElementById('play-goto-collection-always').classList.add('hidden');
  const oldBtn = document.querySelector('.play-start-btn');
  if (oldBtn) oldBtn.remove();
}

function renderPlayDeckGrid() {
  savedDecks = JSON.parse(localStorage.getItem('hs_decks') || '[]');
  const grid = document.getElementById('play-deck-grid');
  const noDecks = document.getElementById('play-no-decks');
  grid.innerHTML = '';
  playSelectedDeckIdx = -1;

  const complete = savedDecks.filter(d => d.cards.length === 30);

  if (complete.length === 0) {
    grid.classList.add('hidden');
    document.getElementById('play-goto-collection-always').classList.add('hidden');
    noDecks.classList.remove('hidden');
    const oldBtn = document.querySelector('.play-start-btn');
    if (oldBtn) oldBtn.remove();
    return;
  }

  grid.classList.remove('hidden');
  document.getElementById('play-goto-collection-always').classList.remove('hidden');
  noDecks.classList.add('hidden');

  savedDecks.forEach((deck, i) => {
    if (deck.cards.length !== 30) return;
    const card = document.createElement('div');
    card.className = 'play-deck-card';
    card.innerHTML = `
      <div class="play-deck-icon">${HERO_ICONS[deck.className] || '📦'}</div>
      <div class="play-deck-name">${deck.name}</div>
      <div class="play-deck-meta">${deck.cards.length}/30 · ${CLASS_NAMES_ZH[deck.className] || deck.className}</div>
    `;
    card.addEventListener('click', () => {
      document.querySelectorAll('.play-deck-card').forEach(c => c.classList.remove('selected'));
      card.classList.add('selected');
      playSelectedDeckIdx = i;
      const btn = document.querySelector('.play-start-btn');
      if (btn) btn.classList.remove('disabled');
    });
    grid.appendChild(card);
  });

  // Add start button if not already present
  let startBtn = document.querySelector('.play-start-btn');
  if (!startBtn) {
    startBtn = document.createElement('button');
    startBtn.className = 'play-start-btn disabled';
    startBtn.textContent = '⚔️ 开始对战';
    startBtn.addEventListener('click', async () => {
      if (playSelectedDeckIdx < 0) return;
      const deck = savedDecks[playSelectedDeckIdx];
      if (!deck || deck.cards.length !== 30) return;
      await startGameWithDeck(deck.className, deck.cards);
    });
    document.querySelector('.play-layout').appendChild(startBtn);
  } else {
    startBtn.classList.add('disabled');
  }
}

document.getElementById('play-goto-collection')?.addEventListener('click', async () => {
  showScreen('deckbuilder-screen');
  try {
    await ensureConnection();
    dbAllCards = await connection.invoke('GetAvailableCards', dbBrowseClass);
    dbRenderTabs();
    dbApplyFilters();
    dbRenderSavedDecks();
  } catch (err) { log(`❌ ${err.message}`); }
});

document.getElementById('play-goto-collection-always')?.addEventListener('click', async () => {
  showScreen('deckbuilder-screen');
  try {
    await ensureConnection();
    dbAllCards = await connection.invoke('GetAvailableCards', dbBrowseClass);
    dbRenderTabs();
    dbApplyFilters();
    dbRenderSavedDecks();
  } catch (err) { log(`❌ ${err.message}`); }
});

// ── Tab Rendering ──

function dbRenderTabs() {
  const tabsEl = document.getElementById('db-tabs');
  tabsEl.innerHTML = '';

  if (dbMode === 'browse') {
    // Show all class tabs + neutral tab
    ALL_CLASSES.forEach(cls => {
      const btn = document.createElement('button');
      btn.className = `db-tab${cls === dbBrowseClass && !dbBrowseNeutral ? ' active' : ''}`;
      btn.dataset.tab = cls;
      btn.textContent = `${HERO_ICONS[cls] || ''} ${CLASS_NAMES_ZH[cls] || cls}`;
      btn.addEventListener('click', async () => {
        dbBrowseClass = cls;
        dbBrowseNeutral = false;
        dbRenderTabs();
        try {
          dbAllCards = await connection.invoke('GetAvailableCards', cls);
          dbApplyFilters();
        } catch (e) { log(`❌ 加载失败: ${e.message}`); }
      });
      tabsEl.appendChild(btn);
    });
    // Neutral tab
    const neutralBtn = document.createElement('button');
    neutralBtn.className = `db-tab${dbBrowseNeutral ? ' active' : ''}`;
    neutralBtn.dataset.tab = 'neutral';
    neutralBtn.textContent = '⚪ 中立';
    neutralBtn.addEventListener('click', async () => {
      dbBrowseNeutral = true;
      dbRenderTabs();
      try {
        // Load any class to get neutral cards (they are included in every class query)
        dbAllCards = await connection.invoke('GetAvailableCards', dbBrowseClass);
        dbApplyFilters();
      } catch (e) { log(`❌ 加载失败: ${e.message}`); }
    });
    tabsEl.appendChild(neutralBtn);
  } else {
    // Deck editing: only [class] + [neutral]
    const classTab = document.createElement('button');
    classTab.className = `db-tab${dbCurrentTab === 'class' ? ' active' : ''}`;
    classTab.dataset.tab = 'class';
    classTab.textContent = `${HERO_ICONS[dbPlayerClass] || ''} ${CLASS_NAMES_ZH[dbPlayerClass] || dbPlayerClass}`;
    classTab.addEventListener('click', () => { dbCurrentTab = 'class'; dbRenderTabs(); dbApplyFilters(); });

    const neutralTab = document.createElement('button');
    neutralTab.className = `db-tab${dbCurrentTab === 'neutral' ? ' active' : ''}`;
    neutralTab.dataset.tab = 'neutral';
    neutralTab.textContent = '⚪ 中立卡';
    neutralTab.addEventListener('click', () => { dbCurrentTab = 'neutral'; dbRenderTabs(); dbApplyFilters(); });

    tabsEl.appendChild(classTab);
    tabsEl.appendChild(neutralTab);
  }
}

// ── Saved Decks Panel ──

function dbRenderSavedDecks() {
  const listEl = document.getElementById('db-saved-list');
  listEl.innerHTML = '';

  savedDecks.forEach((deck, i) => {
    const el = document.createElement('div');
    el.className = 'db-saved-deck';
    el.innerHTML = `
      <span class="saved-icon">${HERO_ICONS[deck.className] || '📦'}</span>
      <div class="saved-info">
        <div class="saved-name">${deck.name}</div>
        <div class="saved-count">${deck.cards.length}/30 · ${CLASS_NAMES_ZH[deck.className] || deck.className}</div>
      </div>
      <button class="saved-delete" data-idx="${i}">✕</button>
    `;
    // Delete button
    el.querySelector('.saved-delete').addEventListener('click', (e) => {
      e.stopPropagation();
      savedDecks.splice(i, 1);
      localStorage.setItem('hs_decks', JSON.stringify(savedDecks));
      dbRenderSavedDecks();
    });
    // Click to edit
    el.addEventListener('click', () => {
      dbPlayerClass = deck.className;
      dbDeck = deck.cards.map(cardId => {
        const found = dbAllCards.find(c => (c.cardId || c.CardId) === cardId);
        return found ? { ...found, cardId } : { cardId, name: cardId, cost: 0, rarity: 'Free' };
      });
      dbEditingDeckIndex = i;
      enterDeckEditMode();
    });
    listEl.appendChild(el);
  });
}

let dbEditingDeckIndex = -1; // -1 = new deck

// ── New Deck Button ──

document.getElementById('db-new-deck-btn').addEventListener('click', () => {
  document.getElementById('class-picker-modal').classList.remove('hidden');
});

document.getElementById('class-picker-cancel').addEventListener('click', () => {
  document.getElementById('class-picker-modal').classList.add('hidden');
});

// Class picker modal buttons
document.querySelectorAll('#class-picker-grid .class-btn').forEach(btn => {
  btn.addEventListener('click', async () => {
    dbPlayerClass = btn.dataset.class;
    dbDeck = [];
    dbEditingDeckIndex = -1;
    document.getElementById('class-picker-modal').classList.add('hidden');
    await enterDeckEditMode();
  });
});

async function enterDeckEditMode() {
  dbMode = 'edit';
  dbCurrentTab = 'class';
  dbCostFilter = 'all';
  dbSearchText = '';
  document.getElementById('db-search').value = '';

  // Switch right panel
  document.getElementById('db-decks-panel').classList.add('hidden');
  document.getElementById('db-edit-panel').classList.remove('hidden');

  // Update header
  document.getElementById('db-hero-icon').textContent = HERO_ICONS[dbPlayerClass] || '🔮';
  document.getElementById('db-deck-name').textContent = `${CLASS_NAMES_ZH[dbPlayerClass] || dbPlayerClass}卡组`;

  // Load cards for this class
  try {
    dbAllCards = await connection.invoke('GetAvailableCards', dbPlayerClass);
    log(`加载了 ${dbAllCards.length} 张 ${CLASS_NAMES_ZH[dbPlayerClass]} 卡牌`);
  } catch (e) { log(`❌ ${e.message}`); }

  dbRenderTabs();
  dbApplyFilters();
  dbUpdateDeckUI();

  // Reset mana filter buttons
  document.querySelectorAll('.db-mana-btn').forEach(b => b.classList.remove('active'));
  document.querySelector('.db-mana-btn[data-cost="all"]').classList.add('active');
}

// Back to browse mode
document.getElementById('db-back-btn').addEventListener('click', () => {
  dbMode = 'browse';
  document.getElementById('db-decks-panel').classList.remove('hidden');
  document.getElementById('db-edit-panel').classList.add('hidden');
  dbRenderTabs();
  dbRenderSavedDecks();
});

// ── Deck Builder Logic ──

const CARDS_PER_PAGE = 8;

function dbApplyFilters() {
  let cards = dbAllCards;

  // Tab filter: class vs neutral
  if (dbMode === 'edit') {
    if (dbCurrentTab === 'class') {
      cards = cards.filter(c => (c.className || c.ClassName) !== 'Neutral');
    } else {
      cards = cards.filter(c => (c.className || c.ClassName) === 'Neutral');
    }
  } else if (dbMode === 'browse') {
    if (dbBrowseNeutral) {
      cards = cards.filter(c => (c.className || c.ClassName) === 'Neutral');
    } else {
      cards = cards.filter(c => (c.className || c.ClassName) !== 'Neutral');
    }
  }

  // Mana filter
  if (dbCostFilter !== 'all') {
    const costVal = parseInt(dbCostFilter);
    if (costVal === 7) {
      cards = cards.filter(c => (c.cost ?? c.Cost ?? 0) >= 7);
    } else {
      cards = cards.filter(c => (c.cost ?? c.Cost ?? 0) === costVal);
    }
  }

  // Search filter
  if (dbSearchText) {
    const q = dbSearchText.toLowerCase();
    cards = cards.filter(c => {
      const name = (c.name || c.Name || '').toLowerCase();
      const desc = (c.description || '').toLowerCase();
      const id = (c.cardId || c.CardId || '').toLowerCase();
      return name.includes(q) || desc.includes(q) || id.includes(q);
    });
  }

  dbFilteredCards = cards;
  dbCurrentPage = 0;
  dbRenderPage();
}

function dbRenderPage() {
  const container = document.getElementById('db-page');
  container.innerHTML = '';

  const totalPages = Math.max(1, Math.ceil(dbFilteredCards.length / CARDS_PER_PAGE));
  if (dbCurrentPage >= totalPages) dbCurrentPage = totalPages - 1;
  if (dbCurrentPage < 0) dbCurrentPage = 0;

  const start = dbCurrentPage * CARDS_PER_PAGE;
  const pageCards = dbFilteredCards.slice(start, start + CARDS_PER_PAGE);

  pageCards.forEach(c => {
    const cardId = c.cardId || c.CardId;
    const name = c.name || c.Name || cardId;
    const cost = c.cost ?? c.Cost ?? 0;
    const atk = c.atk ?? c.Atk ?? 0;
    const health = c.health ?? c.Health;
    const durability = c.durability ?? c.Durability;
    const type = c.type || c.Type || 'Minion';
    const rarity = c.rarity || c.Rarity || 'Free';
    const desc = c.description || '';

    const count = dbMode === 'edit' ? dbDeck.filter(d => d.cardId === cardId).length : 0;
    const maxCount = rarity === 'Legendary' ? 1 : 2;
    const isMaxed = dbMode === 'edit' && (count >= maxCount || dbDeck.length >= 30);

    const el = document.createElement('div');
    el.className = `db-card${isMaxed ? ' maxed' : ''} rarity-${rarity}`;

    const typeName = type === 'Minion' ? '随从' : type === 'Spell' ? '法术' : type === 'Weapon' ? '武器' : type;

    let statsHtml = '';
    if (type === 'Minion') {
      statsHtml = `<div class="card-stats"><span class="card-atk">${atk}</span><span class="card-hp">${health ?? 0}</span></div>`;
    } else if (type === 'Weapon') {
      statsHtml = `<div class="card-stats"><span class="card-atk">${atk}</span><span class="card-hp">${durability ?? 0}</span></div>`;
    }

    el.innerHTML = `
      <div class="card-cost">${cost}</div>
      <div class="db-card-name">${name}</div>
      <div class="db-card-type">${typeName}</div>
      ${desc ? `<div class="db-card-desc">${desc}</div>` : ''}
      ${statsHtml}
      <div class="db-rarity ${rarity}"></div>
    `;

    if (dbMode === 'edit' && !isMaxed) {
      el.addEventListener('click', () => dbAddCard(c));
    }

    container.appendChild(el);
  });

  // Fill empty slots to keep grid stable
  for (let i = pageCards.length; i < CARDS_PER_PAGE; i++) {
    const empty = document.createElement('div');
    empty.className = 'db-card';
    empty.style.visibility = 'hidden';
    container.appendChild(empty);
  }

  document.getElementById('db-page-info').textContent = `第 ${dbCurrentPage + 1} / ${totalPages} 页`;
}

function dbAddCard(card) {
  const cardId = card.cardId || card.CardId;
  const rarity = card.rarity || card.Rarity || 'Free';
  const count = dbDeck.filter(d => d.cardId === cardId).length;
  const maxCount = rarity === 'Legendary' ? 1 : 2;
  if (count >= maxCount || dbDeck.length >= 30) return;

  dbDeck.push({ ...card, cardId });
  dbUpdateDeckUI();
  dbRenderPage();
}

function dbRemoveCard(cardId) {
  const idx = dbDeck.findIndex(d => d.cardId === cardId);
  if (idx >= 0) {
    dbDeck.splice(idx, 1);
    dbUpdateDeckUI();
    dbRenderPage();
  }
}

function dbUpdateDeckUI() {
  const sorted = [...new Set(dbDeck.map(d => d.cardId))];
  const entries = sorted.map(cardId => {
    const card = dbDeck.find(d => d.cardId === cardId);
    const count = dbDeck.filter(d => d.cardId === cardId).length;
    return { ...card, count };
  }).sort((a, b) => {
    const ca = a.cost ?? a.Cost ?? 0;
    const cb = b.cost ?? b.Cost ?? 0;
    if (ca !== cb) return ca - cb;
    return (a.name || '').localeCompare(b.name || '');
  });

  const listEl = document.getElementById('db-deck-list');
  listEl.innerHTML = '';
  entries.forEach(e => {
    const cost = e.cost ?? e.Cost ?? 0;
    const name = e.name || e.Name || e.cardId;
    const rarity = e.rarity || e.Rarity || 'Free';
    const isLegendary = rarity === 'Legendary';

    const entry = document.createElement('div');
    entry.className = 'db-deck-entry fly-in';
    entry.innerHTML = `
      <div class="db-entry-cost">${cost}</div>
      <div class="db-entry-name">${name}</div>
      ${isLegendary ? '<span class="db-entry-legendary">★</span>'
        : e.count > 1 ? `<span class="db-entry-count">${e.count}</span>` : ''}
    `;
    entry.addEventListener('click', () => dbRemoveCard(e.cardId));
    listEl.appendChild(entry);
  });

  document.getElementById('db-count').textContent = dbDeck.length;

  const doneBtn = document.getElementById('db-done-btn');
  if (dbDeck.length === 30) {
    doneBtn.classList.remove('disabled');
  } else {
    doneBtn.classList.add('disabled');
  }

  // Mana curve
  const curve = [0, 0, 0, 0, 0, 0, 0, 0];
  dbDeck.forEach(d => { curve[Math.min(d.cost ?? d.Cost ?? 0, 7)]++; });
  const maxBar = Math.max(...curve, 1);
  document.querySelectorAll('.db-curve-col').forEach(col => {
    const idx = parseInt(col.dataset.cost);
    const bar = col.querySelector('.db-curve-bar');
    bar.style.height = Math.max(2, (curve[idx] / maxBar) * 50) + 'px';
  });
}

// ── Deck Builder Events ──

// Page nav
document.getElementById('db-prev-page').addEventListener('click', () => {
  if (dbCurrentPage > 0) { dbCurrentPage--; dbRenderPage(); }
});
document.getElementById('db-next-page').addEventListener('click', () => {
  const totalPages = Math.ceil(dbFilteredCards.length / CARDS_PER_PAGE);
  if (dbCurrentPage < totalPages - 1) { dbCurrentPage++; dbRenderPage(); }
});

// Mana filter
document.querySelectorAll('.db-mana-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.db-mana-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    dbCostFilter = btn.dataset.cost;
    dbApplyFilters();
  });
});

// Search
document.getElementById('db-search').addEventListener('input', (e) => {
  dbSearchText = e.target.value;
  dbApplyFilters();
});

// Quick fill
document.getElementById('db-fill-btn').addEventListener('click', () => {
  if (dbDeck.length >= 30) return;
  const remaining = 30 - dbDeck.length;
  const eligible = dbAllCards.filter(c => {
    const cardId = c.cardId || c.CardId;
    const rarity = c.rarity || c.Rarity || 'Free';
    const maxCount = rarity === 'Legendary' ? 1 : 2;
    return dbDeck.filter(d => d.cardId === cardId).length < maxCount;
  });
  eligible.sort((a, b) => (a.cost ?? 0) - (b.cost ?? 0));
  for (let i = 0; i < remaining && eligible.length > 0; i++) {
    const idx = Math.floor(Math.random() * Math.min(eligible.length, 30));
    const card = eligible[idx];
    const cardId = card.cardId || card.CardId;
    dbDeck.push({ ...card, cardId });
    const rarity = card.rarity || card.Rarity || 'Free';
    if (dbDeck.filter(d => d.cardId === cardId).length >= (rarity === 'Legendary' ? 1 : 2)) {
      eligible.splice(idx, 1);
    }
  }
  dbUpdateDeckUI();
  dbRenderPage();
});

// Save deck
document.getElementById('db-done-btn').addEventListener('click', () => {
  if (dbDeck.length !== 30) return;
  const deckData = {
    name: `${CLASS_NAMES_ZH[dbPlayerClass] || dbPlayerClass}卡组`,
    className: dbPlayerClass,
    cards: dbDeck.map(d => d.cardId),
  };
  if (dbEditingDeckIndex >= 0) {
    savedDecks[dbEditingDeckIndex] = deckData;
  } else {
    savedDecks.push(deckData);
  }
  localStorage.setItem('hs_decks', JSON.stringify(savedDecks));
  log(`💾 卡组已保存: ${deckData.name}`);

  // Return to browse
  dbMode = 'browse';
  document.getElementById('db-decks-panel').classList.remove('hidden');
  document.getElementById('db-edit-panel').classList.add('hidden');
  dbRenderTabs();
  dbRenderSavedDecks();
});

// Start game with a saved deck
async function startGameWithDeck(className, cardIds) {
  try {
    await ensureConnection();

    if (gameMode === 'pvp') {
      // PvP mode: submit deck (both create & join use this)
      log(`提交卡组: ${CLASS_NAMES_ZH[className]}`);
      await connection.invoke('SubmitDeck', className, cardIds);
      // Show waiting state
      document.getElementById('play-deck-grid').classList.add('hidden');
      document.getElementById('deck-subtitle')?.classList.add('hidden');
      document.getElementById('pvp-waiting').classList.remove('hidden');
      const oldBtn = document.querySelector('.play-start-btn');
      if (oldBtn) oldBtn.remove();
      log('已提交卡组，等待对手...');
    } else {
      // AI mode
      log(`使用 ${CLASS_NAMES_ZH[className]} 卡组开始 AI 游戏`);
      showScreen('game-screen');
      await connection.invoke('NewGame', className, 'Mage', cardIds);
      log('已发送 NewGame 请求');
    }
  } catch (err) {
    log(`❌ 连接失败: ${err.message || err}`);
  }
}

// ── Mulligan ──

function renderMulligan(state) {
  const container = document.getElementById('mulligan-cards');
  container.innerHTML = '';
  mulliganSelected.clear();

  const player = state.player || state.Player;
  if (!player) return;

  const hand = player.hand || player.Hand || [];
  log(`调度阶段: 显示 ${hand.length} 张初始手牌`);

  hand.forEach((c, i) => {
    const cardId = c.cardId || c.CardId;
    const cardName = c.name || c.Name;
    const cost = c.cost ?? c.Cost ?? 0;
    const type = c.type || c.Type || 'Unknown';
    const atk = c.atk ?? c.Atk;
    const health = c.health ?? c.Health;

    const card = document.createElement('div');
    card.className = 'mulligan-card';
    card.dataset.index = i;

    let statsHtml = '';
    if (type === 'Minion' && atk != null) {
      statsHtml = `
        <div class="card-stats">
          <span class="card-atk">${atk}</span>
          <span class="card-hp">${health}</span>
        </div>
      `;
    }

    const typeName = type === 'Minion' ? '随从' : type === 'Spell' ? '法术' : type === 'Weapon' ? '武器' : type;

    card.innerHTML = `
      <div class="card-cost">${cost}</div>
      <div class="card-name">${getCardName(cardId, cardName)}</div>
      <div class="card-type">${typeName}</div>
      ${statsHtml}
    `;

    card.addEventListener('click', () => {
      if (mulliganSelected.has(i)) {
        mulliganSelected.delete(i);
        card.classList.remove('mulligan-selected');
      } else {
        mulliganSelected.add(i);
        card.classList.add('mulligan-selected');
      }
      log(`调度: 已选 ${mulliganSelected.size} 张替换`);
    });

    container.appendChild(card);
  });

  // Show mulligan overlay
  document.getElementById('mulligan-overlay').classList.remove('hidden');
}

document.getElementById('mulligan-confirm-btn').addEventListener('click', () => {
  if (!connection) return;
  const indices = Array.from(mulliganSelected);
  log(`确认调度: 替换 ${indices.length} 张牌`);
  connection.invoke('Mulligan', indices);
  document.getElementById('mulligan-overlay').classList.add('hidden');
});

// ══════════════════════════════════════════
// History Log & Action Feedback System
// ══════════════════════════════════════════

const entityNameMap = {}; // entityId → display name

function buildEntityNameMap(state) {
  const player = state.player || state.Player;
  const opponent = state.opponent || state.Opponent;
  if (!player || !opponent) return;

  // Player hero
  const myHero = player.hero || player.Hero;
  if (myHero) {
    entityNameMap[myHero.entityId ?? myHero.EntityId] = getCardName(myHero.cardId || myHero.CardId);
    // Hero power
    const myHp = myHero.heroPower || myHero.HeroPower;
    if (myHp) entityNameMap[myHp.entityId ?? myHp.EntityId] = '英雄技能';
  }
  // Opponent hero
  const oppHero = opponent.hero || opponent.Hero;
  if (oppHero) {
    entityNameMap[oppHero.entityId ?? oppHero.EntityId] = getCardName(oppHero.cardId || oppHero.CardId);
    const oppHp = oppHero.heroPower || oppHero.HeroPower;
    if (oppHp) entityNameMap[oppHp.entityId ?? oppHp.EntityId] = '对手英雄技能';
  }

  // Player minions
  for (const m of (player.field || player.Field || [])) {
    entityNameMap[m.entityId ?? m.EntityId] = m.name || m.Name || getCardName(m.cardId || m.CardId);
  }
  // Opponent minions
  for (const m of (opponent.field || opponent.Field || [])) {
    entityNameMap[m.entityId ?? m.EntityId] = m.name || m.Name || getCardName(m.cardId || m.CardId);
  }
  // Player hand
  for (const c of (player.hand || player.Hand || [])) {
    entityNameMap[c.entityId ?? c.EntityId] = c.name || c.Name || getCardName(c.cardId || c.CardId);
  }
  // Players themselves
  entityNameMap[player.entityId ?? player.EntityId] = player.name || player.Name || '你';
  entityNameMap[opponent.entityId ?? opponent.EntityId] = opponent.name || opponent.Name || '对手';
}

function eName(entityId) {
  return entityNameMap[entityId] || `#${entityId}`;
}

function addHistoryEntry(icon, cssClass, html, isOpponent = false) {
  const container = document.getElementById('history-entries');
  if (!container) return;
  const entry = document.createElement('div');
  entry.className = `history-entry ${cssClass}${isOpponent ? ' h-opponent' : ' h-player'}`;
  entry.innerHTML = `<span class="h-icon">${icon}</span><span class="h-text">${html}</span>`;
  container.appendChild(entry);
  // Auto-scroll to bottom
  container.scrollTop = container.scrollHeight;
  // Limit to 100 entries
  while (container.children.length > 100) container.removeChild(container.firstChild);
}

function processActions(actions) {
  let blockStack = [];
  for (const a of actions) {
    const type = a.type || a.Type;
    const blockType = a.blockType ?? a.BlockType;
    const sourceId = a.sourceId ?? a.SourceId;
    const targetId = a.targetId ?? a.TargetId;
    const entityId = a.entityId ?? a.EntityId;

    switch (type) {
      case 'BlockStart': {
        blockStack.push({ blockType, sourceId, targetId });
        // Update entity name from sourceCardId if not already known
        const sourceCardId = a.sourceCardId ?? a.SourceCardId;
        let wasUnknown = false;
        if (sourceCardId && sourceId && !entityNameMap[sourceId]) {
          entityNameMap[sourceId] = getCardName(sourceCardId);
          wasUnknown = true; // This entity was not in our hand/field — it's from the opponent
        }
        const src = eName(sourceId);
        const tgt = targetId ? eName(targetId) : null;

        // BlockType enum: Attack=1, Joust=2, Power=3, Script=4, Trigger=5, Deaths=6, Play=7
        switch (blockType) {
          case 7: // PLAY (playing a card or hero power)
          case 'Play': {
            // Distinguish hero power from card play
            const isHeroPower = src === '英雄技能' || src === '对手英雄技能';
            if (isHeroPower) {
              addHistoryEntry('🔮', 'h-power',
                `<span class="h-name">${src}</span>` + (tgt ? ` → <span class="h-name">${tgt}</span>` : ''), wasUnknown);
            } else {
              addHistoryEntry('🃏', 'h-play',
                `<span class="h-name">${src}</span> 打出` + (tgt ? ` → <span class="h-name">${tgt}</span>` : ''), wasUnknown);
              // Card reveal for opponent plays (wasUnknown = entity wasn't in our hand/field)
              if (wasUnknown) {
                showCardReveal(src);
              }
            }
            break;
          }
          case 1: // ATTACK
          case 'Attack':
            if (tgt) {
              addHistoryEntry('⚔️', 'h-attack',
                `<span class="h-name">${src}</span> 攻击 <span class="h-name">${tgt}</span>`, wasUnknown);
              showAttackEffect(sourceId, targetId);
            }
            break;
          case 5: // TRIGGER
          case 'Trigger':
            addHistoryEntry('⚡', 'h-trigger',
              `<span class="h-name">${src}</span> 触发效果`, wasUnknown);
            showTriggerGlow(sourceId);
            break;
          case 3: // POWER (card effect execution, not hero power)
          case 'Power':
            // This is the card's internal effect block — usually don't log
            break;
          case 6: // DEATHS
          case 'Deaths':
            // Deaths processing block — individual deaths logged via TagChange
            break;
        }
        break;
      }
      case 'BlockEnd':
        blockStack.pop();
        break;
      case 'TagChange': {
        const tag = a.tag ?? a.Tag;
        const oldVal = a.oldValue ?? a.OldValue ?? 0;
        const newVal = a.newValue ?? a.NewValue ?? 0;

        // DAMAGE tag (44)
        if ((tag === 44 || tag === 'Damage') && newVal > oldVal) {
          const dmg = newVal - oldVal;
          addHistoryEntry('💥', 'h-damage',
            `<span class="h-name">${eName(entityId)}</span> 受到 ${dmg} 点伤害`);
          showDamagePopup(entityId, dmg);
        }
        // HEALING tag: Health increased (not by damage decrease)
        if ((tag === 45 || tag === 'Health') && newVal > oldVal) {
          const heal = newVal - oldVal;
          addHistoryEntry('💚', 'h-heal',
            `<span class="h-name">${eName(entityId)}</span> 恢复 ${heal} 点生命`);
          showHealPopup(entityId, heal);
        }
        // ZONE → Graveyard (death)
        if ((tag === 49 || tag === 'Zone') && (newVal === 4 || newVal === 'Graveyard')) {
          addHistoryEntry('💀', 'h-death',
            `<span class="h-name">${eName(entityId)}</span> 死亡`);
        }
        break;
      }
      case 'CreateEntity': {
        const cardId = a.cardId || a.CardId;
        const eid = a.entityId ?? a.EntityId;
        if (cardId) entityNameMap[eid] = getCardName(cardId);
        // If inside a PLAY block, this is a summoned minion
        const parentBlock = blockStack[blockStack.length - 1];
        if (parentBlock && (parentBlock.blockType === 3 || parentBlock.blockType === 'Play')) {
          addHistoryEntry('✨', 'h-summon',
            `<span class="h-name">${getCardName(cardId)}</span> 被召唤`);
        }
        break;
      }
    }
  }
}

function isOpponentEntity(entityId) {
  if (!gameState) return false;
  const opp = gameState.opponent || gameState.Opponent;
  if (!opp) return false;
  // Check hero
  const oppHero = opp.hero || opp.Hero;
  if (oppHero && (oppHero.entityId ?? oppHero.EntityId) === entityId) return true;
  // Check field
  for (const m of (opp.field || opp.Field || [])) {
    if ((m.entityId ?? m.EntityId) === entityId) return true;
  }
  return false;
}

// ── Card Reveal (Phase 2A) ──

let revealTimeout = null;

function showCardReveal(cardName) {
  const overlay = document.getElementById('card-reveal-overlay');
  const reveal = document.getElementById('card-reveal');
  if (!overlay || !reveal) return;

  clearTimeout(revealTimeout);
  overlay.classList.remove('hidden', 'fade-out');
  reveal.innerHTML = `
    <div class="reveal-name">${cardName}</div>
    <div class="reveal-type">对手打出</div>
  `;

  revealTimeout = setTimeout(() => {
    overlay.classList.add('fade-out');
    setTimeout(() => overlay.classList.add('hidden'), 300);
  }, 1200);
}

// ── Damage / Heal Popup (Phase 2B) ──

function showDamagePopup(entityId, amount) {
  const el = document.querySelector(`[data-entity-id="${entityId}"]`);
  if (!el) return;
  const popup = document.createElement('div');
  popup.className = 'damage-popup';
  popup.textContent = `-${amount}`;
  el.style.position = 'relative';
  el.appendChild(popup);
  // Also shake
  el.style.animation = 'none';
  el.offsetHeight; // reflow
  el.style.animation = 'triggerGlow 0.3s ease';
  setTimeout(() => { popup.remove(); el.style.animation = ''; }, 1000);
}

function showHealPopup(entityId, amount) {
  const el = document.querySelector(`[data-entity-id="${entityId}"]`);
  if (!el) return;
  const popup = document.createElement('div');
  popup.className = 'heal-popup';
  popup.textContent = `+${amount}`;
  el.style.position = 'relative';
  el.appendChild(popup);
  setTimeout(() => popup.remove(), 1000);
}

// ── Attack Effect (Phase 2C) ──

function showAttackEffect(attackerId, defenderId) {
  const atk = document.querySelector(`[data-entity-id="${attackerId}"]`);
  const def = document.querySelector(`[data-entity-id="${defenderId}"]`);
  if (!atk || !def) return;
  // Flash on defender
  def.style.animation = 'none';
  def.offsetHeight;
  def.style.animation = 'triggerGlow 0.4s ease';
  setTimeout(() => def.style.animation = '', 500);
}

// ── Trigger Glow (Phase 3B) ──

function showTriggerGlow(entityId) {
  const el = document.querySelector(`[data-entity-id="${entityId}"]`);
  if (!el) return;
  el.classList.add('triggering');
  setTimeout(() => el.classList.remove('triggering'), 800);
}

function formatAction(action) {
  if (!action) return '???';
  switch (action.type || action.Type) {
    case 'BlockStart':
      return `▶ ${action.blockType || action.BlockType} #${action.sourceId || action.SourceId}`;
    case 'BlockEnd':
      return `◀ ${action.blockType || action.BlockType}`;
    case 'TagChange':
      return `📝 E#${action.entityId || action.EntityId} tag→${action.newValue || action.NewValue}`;
    default:
      return JSON.stringify(action).substring(0, 60);
  }
}

// ── Render ──

function renderGameState() {
  if (!gameState) {
    log('renderGameState: gameState is null!');
    return;
  }

  const state = gameState;
  // Support both camelCase and PascalCase (SignalR might use either)
  const player = state.player || state.Player;
  const opponent = state.opponent || state.Opponent;
  const isMyTurn = state.isMyTurn ?? state.IsMyTurn ?? false;
  const turn = state.turn ?? state.Turn ?? 0;
  const gState = state.gameState || state.GameState || '';

  if (!player || !opponent) {
    log(`renderGameState: player=${!!player}, opponent=${!!opponent}`);
    return;
  }

  log(`渲染: 回合${turn}, isMyTurn=${isMyTurn}, 手牌=${(player.hand || player.Hand || []).length}张`);

  // Turn indicator
  const turnEl = document.getElementById('turn-indicator');
  if (isMyTurn) {
    turnEl.textContent = `第 ${turn} 回合 — 你的回合`;
    turnEl.classList.remove('opponent-turn');
  } else {
    turnEl.textContent = `第 ${turn} 回合 — 对手回合`;
    turnEl.classList.add('opponent-turn');
  }

  // Opponent info
  const oppName = opponent.name || opponent.Name || '对手';
  const oppMana = opponent.mana ?? opponent.Mana ?? 0;
  const oppMaxMana = opponent.maxMana ?? opponent.MaxMana ?? 0;
  const oppDeckCount = opponent.deckCount ?? opponent.DeckCount ?? 0;
  const oppHandCount = opponent.handCount ?? opponent.HandCount ?? 0;

  document.getElementById('opponent-name').textContent = oppName;
  document.getElementById('opponent-mana').textContent = oppMana;
  document.getElementById('opponent-max-mana').textContent = oppMaxMana;
  document.getElementById('opponent-deck-count').textContent = oppDeckCount;
  document.getElementById('opponent-hand-count').textContent = oppHandCount;

  // Opponent hero
  const oppHero = opponent.hero || opponent.Hero;
  if (oppHero) {
    const oppHeroEl = document.getElementById('opponent-hero');
    const cardId = oppHero.cardId || oppHero.CardId;
    oppHeroEl.innerHTML = `
      <span>${getCardName(cardId)}</span>
      <div class="hero-hp" id="opponent-hp">${oppHero.health ?? oppHero.Health ?? 30}</div>
      <div class="hero-armor ${(oppHero.armor || oppHero.Armor || 0) > 0 ? '' : 'hidden'}" id="opponent-armor">${oppHero.armor || oppHero.Armor || 0}</div>
    `;
  }

  // Player info
  const myMana = player.mana ?? player.Mana ?? 0;
  const myMaxMana = player.maxMana ?? player.MaxMana ?? 0;

  // Player hero
  const myHero = player.hero || player.Hero;
  if (myHero) {
    const myHeroEl = document.getElementById('player-hero');
    const cardId = myHero.cardId || myHero.CardId;
    myHeroEl.innerHTML = `
      <span>${getCardName(cardId)}</span>
      <div class="hero-hp" id="player-hp">${myHero.health ?? myHero.Health ?? 30}</div>
      <div class="hero-armor ${(myHero.armor || myHero.Armor || 0) > 0 ? '' : 'hidden'}" id="player-armor">${myHero.armor || myHero.Armor || 0}</div>
    `;
  }

  // Player mana (add to hero bar)
  let manaEl = document.getElementById('player-mana-display');
  if (!manaEl) {
    manaEl = document.createElement('div');
    manaEl.id = 'player-mana-display';
    manaEl.className = 'mana-display';
    manaEl.style.cssText = 'font-size:1.1rem;';
    document.querySelector('.player-hero-bar').prepend(manaEl);
  }
  manaEl.innerHTML = `💎 <span style="color:#4fc3f7;font-weight:900">${myMana}/${myMaxMana}</span>`;

  // Opponent Field
  const oppField = opponent.field || opponent.Field || [];
  renderField('opponent-field', oppField, false, isMyTurn);

  // Player Field
  const myField = player.field || player.Field || [];
  renderField('player-field', myField, true, isMyTurn);

  // Player Hand
  const myHand = player.hand || player.Hand || [];
  renderHand(myHand, isMyTurn);

  // Hero Power button
  const hpBtn = document.getElementById('hero-power-btn');
  const heroPower = myHero ? (myHero.heroPower || myHero.HeroPower) : null;
  if (heroPower) {
    document.getElementById('hp-cost').textContent = heroPower.cost ?? heroPower.Cost ?? 2;
    hpBtn.disabled = !isMyTurn || (heroPower.exhausted ?? heroPower.Exhausted) || myMana < (heroPower.cost ?? heroPower.Cost ?? 2);
    // Show hero power name
    const hpCardId = heroPower.cardId || heroPower.CardId;
    const hpCached = hpCardId ? getCardData(hpCardId) : null;
    const hpNameEl = document.getElementById('hp-name');
    if (hpNameEl) {
      hpNameEl.textContent = hpCached?.name || getCardName(hpCardId) || '';
    }
  } else {
    hpBtn.disabled = true;
  }

  // Player secrets
  const playerSecretsEl = document.getElementById('player-secrets');
  const mySecrets = player.secrets || player.Secrets || [];
  playerSecretsEl.innerHTML = '';
  mySecrets.forEach(s => {
    const cardId = s.cardId || s.CardId;
    const cached = cardId ? getCardData(cardId) : null;
    const name = cached?.name || getCardName(cardId) || '奥秘';
    const el = document.createElement('div');
    el.className = 'secret-icon mine';
    el.textContent = `🔒 ${name}`;
    el.title = name;
    playerSecretsEl.appendChild(el);
  });

  // Opponent secrets (hidden names, show count as question marks)
  const oppSecretsEl = document.getElementById('opponent-secrets');
  const oppSecretCount = opponent.secretCount ?? opponent.SecretCount ?? 0;
  oppSecretsEl.innerHTML = '';
  for (let i = 0; i < oppSecretCount; i++) {
    const el = document.createElement('div');
    el.className = 'secret-icon opponent';
    el.textContent = '❓';
    el.title = '对手的奥秘';
    oppSecretsEl.appendChild(el);
  }

  // End turn button
  document.getElementById('end-turn-btn').disabled = !isMyTurn;

  // Game over check
  if (gState === 'Complete') {
    showGameOver();
  }

  // Multi-target battlecry: check for pending target choice
  const pendingChoice = state.pendingTargetChoice || state.PendingTargetChoice;
  if (pendingChoice && pendingChoice.type === 'battlecryTarget') {
    const validIds = pendingChoice.validTargetIds || pendingChoice.ValidTargetIds || [];
    const chosen = pendingChoice.chosenCount ?? pendingChoice.ChosenCount ?? 0;
    const total = pendingChoice.totalNeeded ?? pendingChoice.TotalNeeded ?? 0;
    log(`🎯 多目标战吼: 已选 ${chosen}/${total}, 可选目标 ${validIds.length} 个`);
    enterTargetMode('battlecryTarget', -1, { validTargets: validIds });
    highlightTargets(validIds);
    return; // don't cancelTargeting
  }

  // Clear targeting state
  cancelTargeting();
}

// ── Card Tooltip (hover preview) ──

function showCardTooltip(data, e) {
  const tt = document.getElementById('card-tooltip');
  if (!tt) return;

  const cardId = data.cardId || data.CardId;
  const cached = getCardData(cardId);

  const name = data.name || data.Name || cached?.name || getCardName(cardId);
  const cost = data.cost ?? data.Cost ?? cached?.cost;
  const atk = data.atk ?? data.Atk ?? cached?.atk;
  const health = data.health ?? data.Health ?? cached?.health;
  const durability = data.durability ?? data.Durability ?? cached?.durability;
  const type = data.type || data.Type || cached?.type || 'Minion';
  const rarity = cached?.rarity || 'Free';
  const buffIds = data.buffIds || data.BuffIds || [];

  // Get description from cache and apply spell damage dynamically
  let rawDesc = cached?.description || '';
  const spellPower = getPlayerSpellPower();
  let desc = rawDesc;
  if (spellPower > 0 && rawDesc.includes('$')) {
    desc = applySpellDamage(rawDesc, spellPower);
  } else {
    // Strip $N markers to just N
    desc = desc.replace(/\$(\d+)/g, '$1');
  }

  // Keywords for minions
  const keywords = [];
  if (data.taunt ?? data.Taunt) keywords.push('嘲讽');
  if (data.divineShield ?? data.DivineShield) keywords.push('圣盾');
  if (data.poisonous ?? data.Poisonous) keywords.push('剧毒');
  if (data.stealth ?? data.Stealth) keywords.push('潜行');
  if (data.frozen ?? data.Frozen) keywords.push('冻结');
  if (data.windfury ?? data.Windfury) keywords.push('风怒');
  if (data.deathrattle ?? data.Deathrattle) keywords.push('亡语');
  if (data.spellDamage ?? data.SpellDamage) keywords.push(`法术伤害+${data.spellPowerValue ?? data.SpellPowerValue ?? 1}`);

  const typeName = type === 'Minion' ? '随从' : type === 'Spell' ? '法术' : type === 'Weapon' ? '武器' : type;

  // Build stats HTML
  let statsHtml = '';
  if (atk != null) {
    statsHtml += `<span class="tt-stat tt-atk">⚔️ ${atk}</span>`;
  }
  if (health != null) {
    const maxHp = data.maxHealth ?? data.MaxHealth ?? health;
    const damaged = health < maxHp;
    statsHtml += `<span class="tt-stat tt-hp" ${damaged ? 'style="color:#ff7043"' : ''}>❤️ ${health}${damaged ? `/${maxHp}` : ''}</span>`;
  }
  if (durability != null) {
    statsHtml += `<span class="tt-stat tt-dur">🔧 ${durability}</span>`;
  }

  // Process description — replace *N* with boosted highlight
  let descHtml = desc.replace(/\*(\d+)\*/g, '<span class="tt-boosted">$1</span>');

  // Keywords HTML
  let kwHtml = keywords.map(k => `<span class="tt-kw">${k}</span>`).join('');

  // Buffs HTML — look up buffIds from cache
  let buffsHtml = '';
  if (buffIds.length > 0) {
    const buffItems = buffIds.map(bid => {
      const bc = getCardData(bid);
      const bName = bc?.name || bid;
      const bDesc = bc?.description?.replace(/\$(\d+)/g, '$1')?.replace(/<[^>]*>/g, '') || '';
      return `<div class="tt-buff">✨ ${bName}${bDesc ? ` — ${bDesc}` : ''}</div>`;
    }).join('');
    buffsHtml = `<div class="tt-buffs"><div class="tt-buffs-title">附加效果</div>${buffItems}</div>`;
  }

  tt.className = `card-tooltip rarity-${rarity}`;
  tt.innerHTML = `
    <div class="tt-header">
      ${cost != null ? `<div class="tt-cost">${cost}</div>` : ''}
      <div style="flex:1">
        <div class="tt-name">${name}</div>
        <div class="tt-type">${typeName}</div>
      </div>
    </div>
    <div class="tt-body">
      ${statsHtml ? `<div class="tt-stats">${statsHtml}</div>` : ''}
      ${descHtml ? `<div class="tt-desc">${descHtml}</div>` : ''}
      ${kwHtml ? `<div class="tt-keywords">${kwHtml}</div>` : ''}
      ${buffsHtml}
    </div>
  `;

  // Position near mouse, clamped to viewport
  const pad = 12;
  let x = e.clientX + pad;
  let y = e.clientY + pad;
  // Show first to measure
  tt.style.left = '-9999px';
  tt.style.top = '-9999px';
  // Clamp after measuring
  requestAnimationFrame(() => {
    const r = tt.getBoundingClientRect();
    if (x + r.width > window.innerWidth - pad) x = e.clientX - r.width - pad;
    if (y + r.height > window.innerHeight - pad) y = e.clientY - r.height - pad;
    tt.style.left = x + 'px';
    tt.style.top = y + 'px';
  });
}

function hideCardTooltip() {
  const tt = document.getElementById('card-tooltip');
  if (tt) tt.classList.add('hidden');
}

function renderField(containerId, minions, isPlayerSide, isMyTurn) {
  const container = document.getElementById(containerId);
  container.innerHTML = '';

  for (const m of minions) {
    const eid = m.entityId ?? m.EntityId;
    const cardId = m.cardId || m.CardId;
    const cardName = m.name || m.Name;
    const atk = m.atk ?? m.Atk ?? 0;
    const health = m.health ?? m.Health ?? 0;
    const maxHealth = m.maxHealth ?? m.MaxHealth ?? health;
    const canAttack = m.canAttack ?? m.CanAttack ?? false;
    const taunt = m.taunt ?? m.Taunt ?? false;
    const divineShield = m.divineShield ?? m.DivineShield ?? false;
    const frozen = m.frozen ?? m.Frozen ?? false;
    const stealth = m.stealth ?? m.Stealth ?? false;
    const windfury = m.windfury ?? m.Windfury ?? false;
    const poisonous = m.poisonous ?? m.Poisonous ?? false;
    const silenced = m.silenced ?? m.Silenced ?? false;
    const deathrattle = m.deathrattle ?? m.Deathrattle ?? false;
    const spellDamage = m.spellDamage ?? m.SpellDamage ?? false;

    // Update entity name map
    entityNameMap[eid] = cardName || getCardName(cardId);

    const card = document.createElement('div');
    card.className = 'minion-card';
    card.dataset.entityId = eid;

    if (taunt) card.classList.add('taunt');
    if (divineShield) card.classList.add('divine-shield');
    if (frozen) card.classList.add('frozen');
    if (isPlayerSide && canAttack && isMyTurn) {
      card.classList.add('can-attack');
    }

    // Build keyword icons
    let kwHtml = '';
    if (taunt) kwHtml += '<span class="keyword-icon kw-taunt" title="嘲讽">🛡</span>';
    if (divineShield) kwHtml += '<span class="keyword-icon kw-divine" title="圣盾">✦</span>';
    if (poisonous) kwHtml += '<span class="keyword-icon kw-poison" title="剧毒">☠</span>';
    if (stealth) kwHtml += '<span class="keyword-icon kw-stealth" title="潜行">👁</span>';
    if (frozen) kwHtml += '<span class="keyword-icon kw-frozen" title="冻结">❄</span>';
    if (windfury) kwHtml += '<span class="keyword-icon kw-windfury" title="风怒">🌀</span>';
    if (silenced) kwHtml += '<span class="keyword-icon kw-silenced" title="沉默">🚫</span>';
    if (deathrattle) kwHtml += '<span class="keyword-icon kw-deathrattle" title="亡语">💀</span>';
    if (spellDamage) kwHtml += '<span class="keyword-icon kw-spelldmg" title="法术伤害">🔮</span>';

    card.innerHTML = `
      <div class="minion-name">${getCardName(cardId, cardName)}</div>
      <div class="minion-stats">
        <span class="minion-atk">${atk}</span>
        <span class="minion-hp ${health < maxHealth ? 'damaged' : ''}">${health}</span>
      </div>
      ${kwHtml ? `<div class="keyword-bar">${kwHtml}</div>` : '<div class="keyword-bar"></div>'}
    `;

    card.addEventListener('click', () => handleFieldClick(m, isPlayerSide, card));
    card.addEventListener('mouseenter', (e) => showCardTooltip(m, e));
    card.addEventListener('mouseleave', hideCardTooltip);
    card.addEventListener('mousemove', (e) => {
      const tt = document.getElementById('card-tooltip');
      if (tt && !tt.classList.contains('hidden')) {
        tt.style.left = (e.clientX + 12) + 'px';
        tt.style.top = (e.clientY + 12) + 'px';
      }
    });
    container.appendChild(card);
  }
}

function renderHand(hand, isMyTurn) {
  const container = document.getElementById('player-hand');
  container.innerHTML = '';

  if (!hand || hand.length === 0) {
    log('手牌为空');
    return;
  }

  hand.forEach((c, i) => {
    const eid = c.entityId ?? c.EntityId;
    const cardId = c.cardId || c.CardId;
    const cardName = c.name || c.Name;
    const cost = c.cost ?? c.Cost ?? 0;
    const canPlay = (c.canPlay ?? c.CanPlay ?? false) && isMyTurn;
    const type = c.type || c.Type || 'Unknown';
    const requiresTarget = c.requiresTarget ?? c.RequiresTarget ?? false;
    const atk = c.atk ?? c.Atk;
    const health = c.health ?? c.Health;

    const card = document.createElement('div');
    card.className = 'hand-card';
    card.dataset.index = i;
    card.dataset.entityId = eid;

    if (canPlay) {
      card.classList.add('playable');
    } else {
      card.classList.add('unplayable');
    }

    let statsHtml = '';
    if (type === 'Minion' && atk != null) {
      statsHtml = `
        <div class="card-stats">
          <span class="card-atk">${atk}</span>
          <span class="card-hp">${health}</span>
        </div>
      `;
    }

    const typeName = type === 'Minion' ? '随从' : type === 'Spell' ? '法术' : type === 'Weapon' ? '武器' : type;

    card.innerHTML = `
      <div class="card-cost">${cost}</div>
      <div class="card-name">${getCardName(cardId, cardName)}</div>
      <div class="card-type">${typeName}</div>
      ${statsHtml}
    `;

    card.addEventListener('click', () => handleHandClick(c, i, card));
    card.addEventListener('mouseenter', (e) => showCardTooltip(c, e));
    card.addEventListener('mouseleave', hideCardTooltip);
    card.addEventListener('mousemove', (e) => {
      const tt = document.getElementById('card-tooltip');
      if (tt && !tt.classList.contains('hidden')) {
        tt.style.left = (e.clientX + 12) + 'px';
        tt.style.top = (e.clientY + 12) + 'px';
      }
    });
    container.appendChild(card);
  });

  log(`渲染了 ${hand.length} 张手牌`);
}

// ── Interaction Handlers ──

function handleHandClick(cardData, index, cardEl) {
  const isMyTurn = gameState?.isMyTurn ?? gameState?.IsMyTurn ?? false;
  if (!isMyTurn) return;

  const canPlay = cardData.canPlay ?? cardData.CanPlay ?? false;
  if (!canPlay) return;

  const requiresTarget = cardData.requiresTarget ?? cardData.RequiresTarget ?? false;
  const validTargets = cardData.validTargets || cardData.ValidTargets || [];

  if (requiresTarget && validTargets.length > 0) {
    enterTargetMode('play', index, cardData, cardEl);
  } else {
    log(`打出手牌 #${index}`);
    connection.invoke('PlayCard', index, null);
  }
}

function handleFieldClick(minionData, isPlayerSide, cardEl) {
  const isMyTurn = gameState?.isMyTurn ?? gameState?.IsMyTurn ?? false;
  if (!isMyTurn) return;

  const eid = minionData.entityId ?? minionData.EntityId;

  if (targetMode) {
    if (targetMode === 'play' && selectedCard) {
      const validTargets = selectedCard.data.validTargets || selectedCard.data.ValidTargets || [];
      if (validTargets.includes(eid)) {
        log(`打出手牌 → 目标 E#${eid}`);
        connection.invoke('PlayCard', selectedCard.index, eid);
        cancelTargeting();
      }
      return;
    }
    if (targetMode === 'battlecryTarget' && selectedCard) {
      const validTargets = selectedCard.data.validTargets || [];
      if (validTargets.includes(eid)) {
        log(`战吼目标选择: E#${eid}`);
        connection.invoke('ChooseBattlecryTarget', eid);
        cancelTargeting();
      }
      return;
    }
    if (targetMode === 'attack' && attackingEntity && !isPlayerSide) {
      log(`攻击: E#${attackingEntity.entityId} → E#${eid}`);
      connection.invoke('Attack', attackingEntity.entityId, eid);
      cancelTargeting();
      return;
    }
    if (targetMode === 'heropower') {
      log(`英雄技能 → E#${eid}`);
      connection.invoke('UseHeroPower', eid);
      cancelTargeting();
      return;
    }
    return;
  }

  const canAttack = minionData.canAttack ?? minionData.CanAttack ?? false;
  if (isPlayerSide && canAttack) {
    enterTargetMode('attack', null, null, cardEl, eid);
  }
}

function enterTargetMode(mode, cardIndex, cardData, el, entityId) {
  targetMode = mode;
  document.getElementById('target-overlay').classList.remove('hidden');

  if (mode === 'play') {
    selectedCard = { index: cardIndex, el, data: cardData };
    el.classList.add('selected');
    const validTargets = cardData.validTargets || cardData.ValidTargets || [];
    highlightTargets(validTargets);
  } else if (mode === 'attack') {
    attackingEntity = { entityId, el };
    el.classList.add('attacking');
    const enemyEntityIds = [];
    const opp = gameState.opponent || gameState.Opponent;
    const oppField = opp?.field || opp?.Field || [];
    // Check for taunt minions — if any exist, only they are valid targets
    const taunts = oppField.filter(m => (m.taunt ?? m.Taunt) && !m.stealth);
    if (taunts.length > 0) {
      for (const m of taunts) enemyEntityIds.push(m.entityId ?? m.EntityId);
    } else {
      const oppHero = opp?.hero || opp?.Hero;
      if (oppHero) enemyEntityIds.push(oppHero.entityId ?? oppHero.EntityId);
      for (const m of oppField) enemyEntityIds.push(m.entityId ?? m.EntityId);
    }
    highlightTargets(enemyEntityIds);
  } else if (mode === 'heropower') {
    const allEntityIds = [];
    const p = gameState.player || gameState.Player;
    const o = gameState.opponent || gameState.Opponent;
    const ph = p?.hero || p?.Hero;
    const oh = o?.hero || o?.Hero;
    if (oh) allEntityIds.push(oh.entityId ?? oh.EntityId);
    if (ph) allEntityIds.push(ph.entityId ?? ph.EntityId);
    const pf = p?.field || p?.Field || [];
    const of2 = o?.field || o?.Field || [];
    for (const m of pf) allEntityIds.push(m.entityId ?? m.EntityId);
    for (const m of of2) allEntityIds.push(m.entityId ?? m.EntityId);
    highlightTargets(allEntityIds);
  }
}

function highlightTargets(entityIds) {
  const entityIdSet = new Set(entityIds);
  document.querySelectorAll('.minion-card').forEach(el => {
    if (entityIdSet.has(parseInt(el.dataset.entityId))) {
      el.classList.add('targetable');
    }
  });
  // Heroes
  const opp = gameState.opponent || gameState.Opponent;
  const oppHero = opp?.hero || opp?.Hero;
  if (oppHero && entityIdSet.has(oppHero.entityId ?? oppHero.EntityId)) {
    document.getElementById('opponent-hero').classList.add('targetable');
  }
  const p = gameState.player || gameState.Player;
  const myHero = p?.hero || p?.Hero;
  if (myHero && entityIdSet.has(myHero.entityId ?? myHero.EntityId)) {
    document.getElementById('player-hero').classList.add('targetable');
  }
}

function cancelTargeting() {
  targetMode = null;
  selectedCard = null;
  attackingEntity = null;
  document.getElementById('target-overlay').classList.add('hidden');
  document.querySelectorAll('.targetable').forEach(el => el.classList.remove('targetable'));
  document.querySelectorAll('.selected').forEach(el => el.classList.remove('selected'));
  document.querySelectorAll('.attacking').forEach(el => el.classList.remove('attacking'));
}

// ── Hero interactions ──

document.getElementById('opponent-hero').addEventListener('click', () => {
  const isMyTurn = gameState?.isMyTurn ?? gameState?.IsMyTurn ?? false;
  if (!isMyTurn) return;

  const opp = gameState.opponent || gameState.Opponent;
  const oppHero = opp?.hero || opp?.Hero;
  if (!oppHero) return;
  const eid = oppHero.entityId ?? oppHero.EntityId;

  if (targetMode === 'play' && selectedCard) {
    const validTargets = selectedCard.data.validTargets || selectedCard.data.ValidTargets || [];
    if (validTargets.includes(eid)) {
      connection.invoke('PlayCard', selectedCard.index, eid);
      cancelTargeting();
    }
  } else if (targetMode === 'battlecryTarget' && selectedCard) {
    const validTargets = selectedCard.data.validTargets || [];
    if (validTargets.includes(eid)) {
      connection.invoke('ChooseBattlecryTarget', eid);
      cancelTargeting();
    }
  } else if (targetMode === 'attack' && attackingEntity) {
    log(`攻击英雄: E#${attackingEntity.entityId} → E#${eid}`);
    connection.invoke('Attack', attackingEntity.entityId, eid);
    cancelTargeting();
  } else if (targetMode === 'heropower') {
    connection.invoke('UseHeroPower', eid);
    cancelTargeting();
  }
});

document.getElementById('player-hero').addEventListener('click', () => {
  const isMyTurn = gameState?.isMyTurn ?? gameState?.IsMyTurn ?? false;
  if (!isMyTurn) return;

  const p = gameState.player || gameState.Player;
  const myHero = p?.hero || p?.Hero;
  if (!myHero) return;
  const eid = myHero.entityId ?? myHero.EntityId;

  if (targetMode === 'play' && selectedCard) {
    const validTargets = selectedCard.data.validTargets || selectedCard.data.ValidTargets || [];
    if (validTargets.includes(eid)) {
      connection.invoke('PlayCard', selectedCard.index, eid);
      cancelTargeting();
    }
  } else if (targetMode === 'battlecryTarget' && selectedCard) {
    const validTargets = selectedCard.data.validTargets || [];
    if (validTargets.includes(eid)) {
      connection.invoke('ChooseBattlecryTarget', eid);
      cancelTargeting();
    }
  } else if (targetMode === 'heropower') {
    connection.invoke('UseHeroPower', eid);
    cancelTargeting();
  }
});

// Hero Power
document.getElementById('hero-power-btn').addEventListener('click', () => {
  const isMyTurn = gameState?.isMyTurn ?? gameState?.IsMyTurn ?? false;
  if (!isMyTurn) return;
  const p = gameState.player || gameState.Player;
  const myHero = p?.hero || p?.Hero;
  const heroPower = myHero ? (myHero.heroPower || myHero.HeroPower) : null;
  if (!heroPower) return;
  const requiresTarget = heroPower.requiresTarget ?? heroPower.RequiresTarget ?? false;
  if (requiresTarget) {
    enterTargetMode('heropower', null, null, null, null);
  } else {
    connection.invoke('UseHeroPower', null);
  }
});

// End Turn
document.getElementById('end-turn-btn').addEventListener('click', () => {
  const isMyTurn = gameState?.isMyTurn ?? gameState?.IsMyTurn ?? false;
  if (!isMyTurn) return;
  log('结束回合');
  connection.invoke('EndTurn');
});

// Escape
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && targetMode) cancelTargeting();
});

// Click overlay
document.getElementById('target-overlay').addEventListener('click', (e) => {
  if (e.target.id === 'target-overlay' || e.target.classList.contains('target-prompt')) {
    cancelTargeting();
  }
});

// ── Game Over ──

function showGameOver() {
  const overlay = document.getElementById('gameover-overlay');
  const title = document.getElementById('gameover-title');
  const p = gameState.player || gameState.Player;
  const o = gameState.opponent || gameState.Opponent;
  const myHero = p?.hero || p?.Hero;
  const oppHero = o?.hero || o?.Hero;
  const playerDead = !myHero || (myHero.health ?? myHero.Health ?? 0) <= 0;
  const opponentDead = !oppHero || (oppHero.health ?? oppHero.Health ?? 0) <= 0;

  if (playerDead && !opponentDead) {
    title.textContent = '💀 你输了';
    title.style.background = 'linear-gradient(135deg, #ef5350, #c62828)';
  } else if (opponentDead && !playerDead) {
    title.textContent = '🏆 你赢了！';
    title.style.background = 'linear-gradient(135deg, #f5a623, #ffca28)';
  } else {
    title.textContent = '⚔️ 平局';
    title.style.background = 'linear-gradient(135deg, #f5a623, #ffca28)';
  }
  title.style.webkitBackgroundClip = 'text';
  title.style.webkitTextFillColor = 'transparent';
  overlay.classList.remove('hidden');
}

// ── Action Log ──

function addLog(text, important = false) {
  const logEl = document.getElementById('action-log');
  const entry = document.createElement('div');
  entry.className = `log-entry${important ? ' important' : ''}`;
  entry.textContent = text;
  logEl.appendChild(entry);
  logEl.scrollTop = logEl.scrollHeight;
  while (logEl.children.length > 50) logEl.removeChild(logEl.firstChild);
}
