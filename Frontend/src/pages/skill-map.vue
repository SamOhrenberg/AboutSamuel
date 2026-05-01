<template>
  <v-container fluid class="pa-0 fill-height">
    <div class="explorer-layout">

      <!-- Header -->
      <div class="explorer-header">
        <div class="header-left">
          <h1 class="page-title">Embedding Space Explorer</h1>
          <p class="page-subtitle">
            Samuel's experience mapped in 1536-dimensional AI space, projected to 2D.
            Each point is a project, role, or skill — type a query to find where it lands.
          </p>
        </div>
        <div class="query-bar">
          <span class="caret">&gt;_</span>
          <input v-model="queryText" class="query-input" placeholder="Try: microservices, React, leadership..."
            @keydown.enter="runQuery" />
          <button class="query-btn" :disabled="isQuerying" @click="runQuery">
            {{ isQuerying ? '...' : 'QUERY' }}
          </button>
        </div>
      </div>

      <!-- Content -->
      <div class="explorer-content">
        <!-- Canvas takes full space on mobile -->
        <div class="canvas-wrap" ref="canvasWrap">
          <div class="grid-overlay"></div>
          <canvas ref="canvas" @mousemove="onMouseMove" @mouseleave="onMouseLeave" @mousedown="onMouseDown"
            @mouseup="onMouseUp" @wheel.prevent="onWheel"></canvas>

          <div class="mode-toggle">
            <button v-for="m in modes" :key="m.value" class="mode-btn" :class="{ active: filterMode === m.value }"
              @click="filterMode = m.value">{{ m.label }}</button>
            <button class="mode-btn" @click="scale = 1; offset = { x: 0, y: 0 }">Reset</button>
          </div>

          <div class="instructions" :class="{ hidden: isMobile && queryResult }">
            hover to inspect &nbsp;·&nbsp; type a query above to find nearest neighbors
          </div>

          <!-- Mobile info button -->
          <button v-if="isMobile" class="info-btn" @click="sheetOpen = true">
            <span class="info-icon">⊞</span>
            <span>{{ points.length }} points</span>
          </button>

          <!-- Loading / empty states -->
          <div v-if="loading" class="loading-overlay">
            <div class="loading-text"><span class="loading-caret">&gt;_</span> Loading embedding space...</div>
          </div>
          <div v-if="!loading && points.length === 0" class="loading-overlay">
            <div class="loading-text">No projections found.<br><small>Run "Regenerate Visualization" in the admin
                panel.</small></div>
          </div>

          <!-- Tooltip (desktop only) -->
          <div v-if="hoveredPoint && !isMobile" class="tooltip"
            :style="{ left: tooltipPos.x + 'px', top: tooltipPos.y + 'px' }">
            <div class="tooltip-type" :style="{ color: typeColor(hoveredPoint.entityType) }">
              {{ typeLabel(hoveredPoint.entityType) }}
            </div>
            <div class="tooltip-title">{{ hoveredPoint.label }}</div>
            <div v-if="hoveredPoint.subLabel" class="tooltip-meta">{{ hoveredPoint.subLabel }}</div>
            <div v-if="hoveredPoint.techStack.length" class="tooltip-chips">
              <span v-for="t in hoveredPoint.techStack" :key="t" class="tooltip-chip">{{ t }}</span>
            </div>
          </div>
        </div>

        <!-- Desktop side panel (hidden on mobile) -->
        <div class="side-panel" v-if="!isMobile">
          <!-- keep existing side panel content unchanged -->
        </div>

        <!-- Mobile bottom sheet -->
        <Transition name="sheet">
          <div v-if="isMobile && sheetOpen" class="bottom-sheet" @click.self="sheetOpen = false">
            <div class="sheet-content" ref="sheetContent">
              <div class="sheet-handle" @click="sheetOpen = false"></div>

              <div class="sheet-tabs">
                <button class="sheet-tab" :class="{ active: sheetTab === 'results' }" @click="sheetTab = 'results'">
                  Results{{ queryResult ? ` (${queryResult.neighbors.length})` : '' }}
                </button>
                <button class="sheet-tab" :class="{ active: sheetTab === 'info' }" @click="sheetTab = 'info'">
                  Info
                </button>
              </div>

              <!-- Results tab -->
              <div v-if="sheetTab === 'results'" class="sheet-body">
                <template v-if="queryResult">
                  <div class="query-result-label">
                    Results for: <span class="query-term">"{{ lastQuery }}"</span>
                  </div>
                  <div v-for="(n, i) in queryResult.neighbors" :key="n.entityId" class="neighbor-item">
                    <div class="neighbor-rank">#{{ i + 1 }}</div>
                    <div class="neighbor-info">
                      <div class="neighbor-title">{{ n.label }}</div>
                      <div class="neighbor-type">
                        {{ typeLabel(n.entityType) }}{{ n.subLabel ? ' · ' + n.subLabel : '' }}
                      </div>
                      <div class="score-bar">
                        <div class="score-fill" :style="{ width: (n.score * 100).toFixed(0) + '%' }"></div>
                      </div>
                    </div>
                    <div class="neighbor-score">{{ n.score.toFixed(2) }}</div>
                  </div>
                </template>
                <div v-else class="empty-neighbors">
                  Type a query above and tap QUERY to find semantically similar entries.
                </div>
              </div>

              <!-- Info tab -->
              <div v-if="sheetTab === 'info'" class="sheet-body">
                <div class="stat-grid" style="margin-bottom: 1rem">
                  <div class="stat-item">
                    <div class="stat-value">{{ points.length }}</div>
                    <div class="stat-label">Total Points</div>
                  </div>
                  <div class="stat-item">
                    <div class="stat-value">{{ projectCount }}</div>
                    <div class="stat-label">Projects</div>
                  </div>
                  <div class="stat-item">
                    <div class="stat-value">{{ workCount }}</div>
                    <div class="stat-label">Roles</div>
                  </div>
                  <div class="stat-item">
                    <div class="stat-value">{{ infoCount }}</div>
                    <div class="stat-label">Skills</div>
                  </div>
                </div>
                <div class="panel-title">Legend</div>
                <div v-for="l in legend" :key="l.type" class="legend-item">
                  <div class="legend-dot" :style="{ background: l.color, boxShadow: `0 0 6px ${l.glow}` }"></div>
                  <span>{{ l.label }}</span>
                  <span class="legend-count">{{ l.count }}</span>
                </div>
              </div>
            </div>
          </div>
        </Transition>
      </div>
    </div>
  </v-container>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue';

const API_BASE = import.meta.env.VITE_API_URL || 'https://api.aboutsamuel.com';

// ── State ─────────────────────────────────────────────────────────────────
const canvasWrap = ref(null);
const canvas = ref(null);
const points = ref([]);
const loading = ref(true);
const queryText = ref('');
const lastQuery = ref('');
const queryResult = ref(null);
const isQuerying = ref(false);
const hoveredPoint = ref(null);
const tooltipPos = ref({ x: 0, y: 0 });
const filterMode = ref('all');
const highlightNeighbor = ref(null);
const sheetOpen = ref(false);
const sheetTab = ref('results');
const isMobile = ref(false);

function checkMobile() {
  isMobile.value = window.innerWidth < 768;
}

let ctx = null;
let animHandle = null;
let pulseT = 0;

// ── Constants ─────────────────────────────────────────────────────────────
const COLORS = {
  project: { fill: '#8BE9FD', glow: 'rgba(139,233,253,0.4)', dim: 'rgba(139,233,253,0.45)' },
  work: { fill: '#fdb831', glow: 'rgba(253,184,49,0.4)', dim: 'rgba(253,184,49,0.45)' },
  information: { fill: '#50FA7B', glow: 'rgba(80,250,123,0.3)', dim: 'rgba(80,250,123,0.38)' },
};

const modes = [
  { value: 'all', label: 'All' },
  { value: 'project', label: 'Projects' },
  { value: 'work', label: 'Work' },
  { value: 'information', label: 'Skills' },
];

const clusterLabels = [
  { text: 'Backend<br>Engineering', top: '15%', left: '8%' },
  { text: 'Data &<br>Analytics', top: '60%', left: '6%' },
  { text: 'Frontend &<br>UI', top: '15%', left: '62%' },
  { text: 'Leadership &<br>Arch.', top: '65%', left: '60%' },
  { text: 'DevOps &<br>Cloud', top: '38%', left: '40%' },
];

const legend = computed(() => [
  { type: 'project', label: 'Projects', color: '#8BE9FD', glow: 'rgba(139,233,253,0.5)', count: projectCount.value },
  { type: 'work', label: 'Work Experience', color: '#fdb831', glow: 'rgba(253,184,49,0.5)', count: workCount.value },
  { type: 'information', label: 'Skills & Info', color: '#50FA7B', glow: 'rgba(80,250,123,0.4)', count: infoCount.value },
]);

const projectCount = computed(() => points.value.filter(p => p.entityType === 'project').length);
const workCount = computed(() => points.value.filter(p => p.entityType === 'work').length);
const infoCount = computed(() => points.value.filter(p => p.entityType === 'information').length);

const neighborIds = computed(() => new Set(queryResult.value?.neighbors.map(n => n.entityId) ?? []));

const scale = ref(1);
const offset = ref({ x: 0, y: 0 });
let isPanning = false;
let panStart = { x: 0, y: 0 };
let panOffset = { x: 0, y: 0 };

// ── Data fetching ─────────────────────────────────────────────────────────
async function loadProjections() {
  loading.value = true;
  try {
    const res = await fetch(`${API_BASE}/embedding-visualization`);
    const data = await res.json();
    points.value = data.points ?? [];
  } catch (e) {
    console.error('Failed to load projections', e);
  } finally {
    loading.value = false;
  }
}

async function runQuery() {
  const q = queryText.value.trim();
  if (!q || isQuerying.value) return;
  isQuerying.value = true;
  lastQuery.value = q;
  try {
    const res = await fetch(`${API_BASE}/embedding-visualization/query`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ query: q, topN: 5 }),
    });
    queryResult.value = await res.json();
    // Auto-open sheet on mobile after query
    if (isMobile.value) {
      sheetTab.value = 'results';
      sheetOpen.value = true;
    }
  } catch (e) {
    console.error('Query failed', e);
  } finally {
    isQuerying.value = false;
  }
}

// ── Canvas rendering ──────────────────────────────────────────────────────
function toCanvas(nx, ny) {
  return [
    nx * canvas.value.width * scale.value + offset.value.x,
    ny * canvas.value.height * scale.value + offset.value.y
  ];
}

function onMouseDown(e) {
  isPanning = true;
  panStart = { x: e.clientX - offset.value.x, y: e.clientY - offset.value.y };
}

function onMouseUp() { isPanning = false; }

function onWheel(e) {
  e.preventDefault();
  const delta = e.deltaY > 0 ? 0.9 : 1.1;
  scale.value = Math.min(Math.max(scale.value * delta, 0.5), 5);
}

function draw() {
  if (!ctx || !canvas.value) { animHandle = requestAnimationFrame(draw); return; }
  pulseT += 0.025;
  ctx.clearRect(0, 0, canvas.value.width, canvas.value.height);

  // Cluster labels
  const clusterDefs = [
    { text: ['BACKEND', 'ENGINEERING'], x: 0.08, y: 0.15 },
    { text: ['DATA &', 'ANALYTICS'], x: 0.06, y: 0.60 },
    { text: ['FRONTEND', '& UI'], x: 0.62, y: 0.15 },
    { text: ['LEADERSHIP', '& ARCH.'], x: 0.60, y: 0.65 },
    { text: ['DEVOPS', '& CLOUD'], x: 0.40, y: 0.38 },
  ];

  ctx.font = '700 11px Raleway, sans-serif';
  ctx.letterSpacing = '2px';
  clusterDefs.forEach(cl => {
    const [x, y] = toCanvas(cl.x, cl.y);
    ctx.fillStyle = 'rgba(139,233,253,0.35)';
    cl.text.forEach((line, i) => {
      ctx.fillText(line, x, y + i * 15);
    });
  });

  const visiblePoints = filterMode.value === 'all'
    ? points.value
    : points.value.filter(p => p.entityType === filterMode.value);

  // Connection lines from query point to neighbors
  if (queryResult.value) {
    const [qx, qy] = toCanvas(queryResult.value.queryX, queryResult.value.queryY);
    queryResult.value.neighbors.forEach((n, i) => {
      const pt = points.value.find(p => p.entityId === n.entityId);
      if (!pt) return;
      const [px, py] = toCanvas(pt.x, pt.y);
      ctx.beginPath();
      ctx.moveTo(qx, qy);
      ctx.lineTo(px, py);
      ctx.strokeStyle = `rgba(253,184,49,${0.45 - i * 0.07})`;
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 6]);
      ctx.stroke();
      ctx.setLineDash([]);
    });
  }

  // Draw points
  visiblePoints.forEach(p => {
    const [x, y] = toCanvas(p.x, p.y);
    const col = COLORS[p.entityType] ?? COLORS.information;
    const isNeighbor = neighborIds.value.has(p.entityId);
    const isHL = highlightNeighbor.value === p.entityId || isNeighbor;
    const isHov = hoveredPoint.value?.entityId === p.entityId;
    const pulse = isHL ? Math.sin(pulseT * 2 + p.x * 10) * 0.4 + 0.6 : 1;
    const r = 7 * (isHov ? 1.6 : 1) * (isHL ? 1.2 : 1);

    if (isHov || isHL) {
      const grad = ctx.createRadialGradient(x, y, 0, x, y, r * 4);
      grad.addColorStop(0, col.glow.replace('0.4', String((0.35 * pulse).toFixed(2))));
      grad.addColorStop(1, 'transparent');
      ctx.beginPath();
      ctx.arc(x, y, r * 4, 0, Math.PI * 2);
      ctx.fillStyle = grad;
      ctx.fill();
    }

    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fillStyle = (isHL || isHov) ? col.fill : col.dim;
    ctx.fill();

    if (p.entityType === 'work' || isHL) {
      ctx.strokeStyle = col.fill;
      ctx.lineWidth = isHL ? 2 : 1;
      ctx.stroke();
    }
  });

  // Query point
  if (queryResult.value) {
    const [qx, qy] = toCanvas(queryResult.value.queryX, queryResult.value.queryY);
    const pulse = Math.sin(pulseT * 3) * 0.3 + 0.7;

    ctx.beginPath();
    ctx.arc(qx, qy, 18 + Math.sin(pulseT * 2) * 4, 0, Math.PI * 2);
    ctx.strokeStyle = `rgba(253,184,49,${(0.15 * pulse).toFixed(2)})`;
    ctx.lineWidth = 1;
    ctx.stroke();

    ctx.beginPath();
    ctx.arc(qx, qy, 10, 0, Math.PI * 2);
    ctx.strokeStyle = `rgba(253,184,49,${(0.4 * pulse).toFixed(2)})`;
    ctx.lineWidth = 1.5;
    ctx.stroke();

    ctx.beginPath();
    ctx.arc(qx, qy, 5, 0, Math.PI * 2);
    ctx.fillStyle = '#fdb831';
    ctx.fill();

    ctx.fillStyle = '#fdb831';
    ctx.font = '600 10px Raleway, sans-serif';
    const labelText = lastQuery.value.length > 20
      ? lastQuery.value.slice(0, 20) + '...'
      : lastQuery.value;
    ctx.fillText(`"${labelText}"`, qx + 8, qy - 8);
  }

  animHandle = requestAnimationFrame(draw);
}

function resize() {
  if (!canvas.value || !canvasWrap.value) return;
  canvas.value.width = canvasWrap.value.clientWidth;
  canvas.value.height = canvasWrap.value.clientHeight;
}

// ── Mouse interaction ─────────────────────────────────────────────────────
function onMouseMove(e) {
  if (!canvas.value) return;

  if (isPanning) {
    offset.value = { x: e.clientX - panStart.x, y: e.clientY - panStart.y };
    return;
  }

  const rect = canvas.value.getBoundingClientRect();
  const mx = e.clientX - rect.left;
  const my = e.clientY - rect.top;

  hoveredPoint.value = null;
  const visiblePoints = filterMode.value === 'all'
    ? points.value
    : points.value.filter(p => p.entityType === filterMode.value);

  for (const p of visiblePoints) {
    const [x, y] = toCanvas(p.x, p.y);
    if (Math.sqrt((mx - x) ** 2 + (my - y) ** 2) < 12) {
      hoveredPoint.value = p;
      break;
    }
  }

  if (hoveredPoint.value) {
    let tx = mx + 14, ty = my - 10;
    if (tx + 250 > canvas.value.width) tx = mx - 260;
    tooltipPos.value = { x: tx, y: ty };
  }
}

function onMouseLeave() { hoveredPoint.value = null; }

function typeColor(type) { return COLORS[type]?.fill ?? '#8BE9FD'; }
function typeLabel(type) {
  return { project: 'Project', work: 'Work Experience', information: 'Skill / Info' }[type] ?? type;
}

onMounted(async () => {
  checkMobile();
  window.addEventListener('resize', checkMobile);
  await loadProjections();
  await nextTick();
  if (canvas.value) {
    ctx = canvas.value.getContext('2d');
    canvas.value.addEventListener('wheel', onWheel, { passive: false });
    canvas.value.addEventListener('mousedown', onMouseDown);
    window.addEventListener('mouseup', onMouseUp);
    resize();
    draw();
  }
  window.addEventListener('resize', resize);
});

onUnmounted(() => {
  window.removeEventListener('resize', checkMobile);
  window.removeEventListener('resize', resize);
  if (animHandle) cancelAnimationFrame(animHandle);
});
</script>

<style scoped>
.explorer-layout {
  display: grid;
  grid-template-rows: auto 1fr;
  height: 100%;
  width: 100%;
}

.explorer-header {
  padding: 1.5rem 2rem 1.25rem;
  border-bottom: 1px solid rgba(139, 233, 253, 0.15);
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 1rem;
}

.page-title {
  font-family: 'Patua One', serif;
  font-size: 1.8rem;
  color: #fff;
  line-height: 1.1;
}

.page-subtitle {
  font-size: 0.85rem;
  color: rgba(224, 242, 242, 0.5);
  margin-top: 0.3rem;
  max-width: 500px;
}

.query-bar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: rgba(139, 233, 253, 0.04);
  border: 1px solid rgba(139, 233, 253, 0.15);
  border-radius: 8px;
  padding: 0.4rem 0.75rem;
  min-width: 300px;
}

.caret {
  font-family: 'Courier Prime', monospace;
  color: #50FA7B;
  font-size: 0.9rem;
  animation: blink 1.2s step-end infinite;
}

@keyframes blink {

  0%,
  100% {
    opacity: 1
  }

  50% {
    opacity: 0
  }
}

.query-input {
  background: none;
  border: none;
  outline: none;
  color: #e0f2f2;
  font-family: 'Raleway', sans-serif;
  font-size: 0.85rem;
  flex: 1;
}

.query-input::placeholder {
  color: rgba(139, 233, 253, 0.3);
  font-style: italic;
}

.query-btn {
  background: rgba(139, 233, 253, 0.08);
  border: 1px solid rgba(139, 233, 253, 0.25);
  border-radius: 5px;
  color: #8BE9FD;
  font-family: 'Courier Prime', monospace;
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.3rem 0.7rem;
  cursor: pointer;
  letter-spacing: 0.06em;
  transition: all 0.15s;
}

.query-btn:hover:not(:disabled) {
  background: rgba(139, 233, 253, 0.15);
  border-color: #8BE9FD;
}

.query-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.explorer-content {
  display: grid;
  grid-template-columns: 1fr 280px;
  overflow: hidden;
  min-height: 0;
}

.canvas-wrap {
  position: relative;
  overflow: hidden;
  background: radial-gradient(ellipse at 40% 50%, rgba(0, 172, 172, 0.06) 0%, transparent 65%),
    radial-gradient(ellipse at 70% 30%, rgba(139, 233, 253, 0.04) 0%, transparent 50%),
    #001a1a;
}

.grid-overlay {
  position: absolute;
  inset: 0;
  pointer-events: none;
  z-index: 0;
  background-image:
    linear-gradient(rgba(139, 233, 253, 0.04) 1px, transparent 1px),
    linear-gradient(90deg, rgba(139, 233, 253, 0.04) 1px, transparent 1px);
  background-size: 60px 60px;
}

canvas {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  z-index: 2;
  cursor: crosshair;
}


.tooltip {
  position: absolute;
  background: rgba(0, 20, 20, 0.96);
  border: 1px solid rgba(139, 233, 253, 0.3);
  border-radius: 8px;
  padding: 0.65rem 0.85rem;
  font-size: 0.78rem;
  pointer-events: none;
  z-index: 10;
  min-width: 180px;
  max-width: 240px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
}

.tooltip-type {
  font-family: 'Courier Prime', monospace;
  font-size: 0.65rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  margin-bottom: 0.3rem;
}

.tooltip-title {
  font-weight: 700;
  color: #fff;
  margin-bottom: 0.2rem;
  line-height: 1.3;
}

.tooltip-meta {
  color: rgba(224, 242, 242, 0.5);
  font-size: 0.72rem;
}

.tooltip-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 3px;
  margin-top: 0.4rem;
}

.tooltip-chip {
  background: rgba(139, 233, 253, 0.08);
  border: 1px solid rgba(139, 233, 253, 0.2);
  border-radius: 3px;
  padding: 0.1rem 0.35rem;
  font-size: 0.62rem;
  color: rgba(139, 233, 253, 0.7);
}

.mode-toggle {
  position: absolute;
  top: 1rem;
  left: 1rem;
  display: flex;
  z-index: 5;
  border: 1px solid rgba(139, 233, 253, 0.15);
  border-radius: 6px;
  overflow: hidden;
}

.mode-btn {
  padding: 0.3rem 0.75rem;
  background: rgba(0, 20, 20, 0.85);
  border: none;
  border-right: 1px solid rgba(139, 233, 253, 0.15);
  color: rgba(224, 242, 242, 0.5);
  font-family: 'Courier Prime', monospace;
  font-size: 0.65rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  cursor: pointer;
  transition: all 0.15s;
}

.mode-btn:last-child {
  border-right: none;
}

.mode-btn.active {
  background: rgba(139, 233, 253, 0.1);
  color: #8BE9FD;
}

.mode-btn:hover:not(.active) {
  background: rgba(139, 233, 253, 0.05);
  color: #e0f2f2;
}

.instructions {
  position: absolute;
  bottom: 1.5rem;
  left: 50%;
  transform: translateX(-50%);
  background: rgba(0, 20, 20, 0.8);
  border: 1px solid rgba(139, 233, 253, 0.15);
  border-radius: 20px;
  padding: 0.4rem 1rem;
  font-size: 0.72rem;
  color: rgba(224, 242, 242, 0.5);
  font-family: 'Courier Prime', monospace;
  z-index: 5;
  white-space: nowrap;
}

.loading-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 6;
  background: rgba(0, 20, 20, 0.6);
}

.loading-text {
  font-family: 'Courier Prime', monospace;
  color: rgba(139, 233, 253, 0.6);
  font-size: 0.9rem;
  text-align: center;
  line-height: 1.6;
}

.loading-caret {
  color: #50FA7B;
  animation: blink 1.2s step-end infinite;
}

/* Side panel */
.side-panel {
  border-left: 1px solid rgba(139, 233, 253, 0.15);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: rgba(10, 30, 30, 0.6);
}

.panel-section {
  padding: 1rem;
  border-bottom: 1px solid rgba(139, 233, 253, 0.1);
}

.neighbors-section {
  border-bottom: none;
  flex: 1;
  overflow-y: auto;
}

.panel-title {
  font-family: 'Courier Prime', monospace;
  font-size: 0.65rem;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: rgba(139, 233, 253, 0.45);
  margin-bottom: 0.75rem;
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.panel-title::before {
  content: '';
  display: block;
  width: 2px;
  height: 10px;
  background: #8BE9FD;
  border-radius: 1px;
}

.stat-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
}

.stat-item {
  background: rgba(139, 233, 253, 0.03);
  border: 1px solid rgba(139, 233, 253, 0.12);
  border-radius: 6px;
  padding: 0.5rem 0.6rem;
}

.stat-value {
  font-family: 'Patua One', serif;
  font-size: 1.3rem;
  color: #8BE9FD;
  line-height: 1;
}

.stat-label {
  font-size: 0.65rem;
  color: rgba(224, 242, 242, 0.5);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin-top: 0.2rem;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin-bottom: 0.5rem;
  font-size: 0.8rem;
  color: rgba(224, 242, 242, 0.7);
}

.legend-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex-shrink: 0;
}

.query-dot {
  background: #fdb831;
  box-shadow: 0 0 10px rgba(253, 184, 49, 0.6);
  width: 12px;
  height: 12px;
}

.legend-count {
  margin-left: auto;
  font-family: 'Courier Prime', monospace;
  font-size: 0.72rem;
  color: rgba(139, 233, 253, 0.4);
}

.query-result-label {
  font-size: 0.72rem;
  color: rgba(224, 242, 242, 0.5);
  margin-bottom: 0.6rem;
  line-height: 1.5;
}

.query-term {
  color: #fdb831;
  font-weight: 600;
}

.neighbor-item {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.5rem 0.4rem;
  border-bottom: 1px solid rgba(139, 233, 253, 0.06);
  cursor: pointer;
  transition: background 0.1s;
  border-radius: 4px;
}

.neighbor-item:hover {
  background: rgba(139, 233, 253, 0.04);
}

.neighbor-item:last-child {
  border-bottom: none;
}

.neighbor-rank {
  font-family: 'Courier Prime', monospace;
  font-size: 0.65rem;
  color: #fdb831;
  font-weight: 700;
  margin-top: 2px;
  flex-shrink: 0;
  width: 18px;
}

.neighbor-info {
  flex: 1;
  min-width: 0;
}

.neighbor-title {
  font-size: 0.78rem;
  font-weight: 600;
  color: #e0f2f2;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.neighbor-type {
  font-size: 0.65rem;
  color: rgba(224, 242, 242, 0.5);
  margin-top: 1px;
}

.score-bar {
  height: 2px;
  background: rgba(139, 233, 253, 0.1);
  border-radius: 1px;
  margin-top: 3px;
  overflow: hidden;
}

.score-fill {
  height: 100%;
  background: linear-gradient(90deg, #00acac, #8BE9FD);
  border-radius: 1px;
  transition: width 0.4s ease;
}

.neighbor-score {
  font-family: 'Courier Prime', monospace;
  font-size: 0.7rem;
  color: #50FA7B;
  flex-shrink: 0;
}

.empty-neighbors {
  font-size: 0.78rem;
  color: rgba(224, 242, 242, 0.35);
  line-height: 1.6;
  font-style: italic;
}

::-webkit-scrollbar {
  width: 4px;
}

::-webkit-scrollbar-track {
  background: transparent;
}

::-webkit-scrollbar-thumb {
  background: rgba(139, 233, 253, 0.2);
  border-radius: 2px;
}

/* Mobile canvas takes full content area */
@media (max-width: 767px) {
  .explorer-content {
    grid-template-columns: 1fr;
  }
  .explorer-header {
    flex-direction: column;
    align-items: stretch;
    padding: 1rem;
  }
  .query-bar {
    min-width: unset;
  }
}

/* Info button */
.info-btn {
  position: absolute;
  bottom: 1.5rem;
  right: 1rem;
  display: flex;
  align-items: center;
  gap: 0.4rem;
  background: rgba(0,20,20,0.85);
  border: 1px solid rgba(139,233,253,0.3);
  border-radius: 20px;
  padding: 0.4rem 0.85rem;
  color: #8BE9FD;
  font-family: 'Courier Prime', monospace;
  font-size: 0.72rem;
  cursor: pointer;
  z-index: 5;
}

.info-icon { font-size: 1rem; }
.instructions.hidden { opacity: 0; }

/* Bottom sheet */
.bottom-sheet {
  position: fixed;
  inset: 0;
  z-index: 50;
  display: flex;
  align-items: flex-end;
  background: rgba(0,0,0,0.5);
}

.sheet-content {
  width: 100%;
  max-height: 70vh;
  background: #0a2a2a;
  border-top: 1px solid rgba(139,233,253,0.25);
  border-radius: 16px 16px 0 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.sheet-handle {
  width: 40px;
  height: 4px;
  background: rgba(139,233,253,0.3);
  border-radius: 2px;
  margin: 0.75rem auto 0;
  cursor: pointer;
  flex-shrink: 0;
}

.sheet-tabs {
  display: flex;
  border-bottom: 1px solid rgba(139,233,253,0.15);
  flex-shrink: 0;
  padding: 0 1rem;
  gap: 0;
}

.sheet-tab {
  padding: 0.75rem 1rem;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  color: rgba(224,242,242,0.5);
  font-family: 'Courier Prime', monospace;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  cursor: pointer;
  transition: all 0.15s;
  margin-bottom: -1px;
}

.sheet-tab.active {
  color: #8BE9FD;
  border-bottom-color: #8BE9FD;
}

.sheet-body {
  flex: 1;
  overflow-y: auto;
  padding: 1rem;
}

/* Sheet transition */
.sheet-enter-active, .sheet-leave-active {
  transition: opacity 0.25s ease;
}
.sheet-enter-active .sheet-content,
.sheet-leave-active .sheet-content {
  transition: transform 0.3s cubic-bezier(0.32, 0.72, 0, 1);
}
.sheet-enter-from, .sheet-leave-to {
  opacity: 0;
}
.sheet-enter-from .sheet-content,
.sheet-leave-to .sheet-content {
  transform: translateY(100%);
}
</style>
