<script setup>
import { ref, watch, nextTick, computed, onMounted, onUnmounted } from 'vue'
import { useChatStore } from '@/stores/chatStore'
import { formatDateTime } from '@/utilities/dateUtils'
import { useRouter } from 'vue-router'

const router = useRouter()
const store = useChatStore()
const chatContainer = ref(null)
const showScrollBtn = ref(false)
const copiedKey = ref(null)

// ── Starter prompts ───────────────────────────────────────
const starterPrompts = [
  "What's Samuel's tech stack?",
  "Tell me about his projects",
  "What's his work experience?",
  "What sets Samuel apart?",
]

const showPrompts = computed(() =>
  store.messageHistory.length === 0 && store.archivedMessageHistory.length === 0
)

// ── Scroll logic ──────────────────────────────────────────
function scrollToBottom() {
  nextTick(() => {
    if (chatContainer.value) {
      chatContainer.value.scrollTop = chatContainer.value.scrollHeight
    }
  })
}

function onChatScroll() {
  if (!chatContainer.value) return
  const { scrollTop, scrollHeight, clientHeight } = chatContainer.value
  showScrollBtn.value = scrollHeight - scrollTop - clientHeight > 100
}

watch(() => store.messageHistory.length, scrollToBottom)
watch(() => store.messageHistory.at(-1)?.message, scrollToBottom)

// ── Send message ──────────────────────────────────────────
async function sendMessage() {
  const redirectToPage = await store.sendMessage()
  if (redirectToPage) router.push(`/${redirectToPage}`)
}

function sendPrompt(prompt) {
  store.message = prompt
  sendMessage()
}

function clearHistory() {
  store.$patch({ messageHistory: [], archivedMessageHistory: [] })
  localStorage.removeItem('samuellm_chat')
}

// ── Copy message ──────────────────────────────────────────
async function copyMessage(messageItem) {
  await navigator.clipboard.writeText(messageItem.message)
  copiedKey.value = messageItem.key ?? messageItem.sentAt
  setTimeout(() => { copiedKey.value = null }, 2000)
}

// ── Expand / collapse ─────────────────────────────────────
function toggleExpand() {
  store.isFullscreen = !store.isFullscreen
  scrollToBottom()
}

function handleKeydown(e) {
  if (e.key === 'Escape' && store.isFullscreen) store.isFullscreen = false
}

// ── localStorage persistence ──────────────────────────────
onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
  try {
    const saved = localStorage.getItem('samuellm_chat')
    if (saved) {
      const parsed = JSON.parse(saved)
      if (parsed?.messageHistory?.length) {
        store.$patch({ messageHistory: parsed.messageHistory })
      }
    }
  } catch (e) { /* ignore */ }
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
})

watch(
  () => store.messageHistory,
  (val) => {
    try {
      localStorage.setItem('samuellm_chat', JSON.stringify({ messageHistory: val }))
    } catch (e) { /* ignore */ }
  },
  { deep: true }
)
</script>

<template>
  <!-- ── Open chat panel ─────────────────────────────────────── -->
  <Transition name="chat-panel">
    <div v-if="store.isOpen" class="chat-panel-wrapper" :class="{ 'is-expanded': store.isFullscreen }">

      <!-- Scanline overlay -->
      <div class="panel-scanlines" aria-hidden="true" />

      <div class="chatbox-container" id="chatbox" role="region" aria-label="Chat with SamuelLM">

        <!-- ── Terminal title bar ── -->
        <div class="chat-titlebar">
          <div class="titlebar-left">
            <span class="titlebar-status-dot" />
            <span class="titlebar-label">SAMUELLM<span class="titlebar-version">_v1.0</span></span>
          </div>
          <div class="titlebar-controls">
            <button class="titlebar-btn" @click="clearHistory" aria-label="Clear chat history"
              title="Clear chat history">
              <v-icon size="16">mdi-delete-outline</v-icon>
            </button>
            <button class="titlebar-btn d-none d-sm-flex" @click="toggleExpand"
              :aria-label="store.isFullscreen ? 'Collapse chat' : 'Expand chat'">
              <v-icon size="16">{{ isExpanded ? 'mdi-arrow-collapse' : 'mdi-arrow-expand' }}</v-icon>
            </button>
            <button class="titlebar-btn titlebar-btn--close" @click="store.isOpen = false" aria-label="Close chat">
              <v-icon size="16">mdi-close</v-icon>
            </button>
          </div>
        </div>

        <!-- ── Chat history ── -->
        <div id="chat-history" ref="chatContainer" @scroll="onChatScroll" aria-live="polite"
          aria-label="Chat message history">

          <!-- Starter prompts -->
          <Transition name="prompts-fade">
            <div v-if="showPrompts" class="starter-prompts">
              <p class="starter-heading"><span class="prompt-caret">&gt;</span> How can I help you today?</p>
              <div class="prompt-grid">
                <button v-for="prompt in starterPrompts" :key="prompt" class="prompt-chip" @click="sendPrompt(prompt)">
                  <span class="prompt-bracket">[</span>{{ prompt }}<span class="prompt-bracket">]</span>
                </button>
              </div>
            </div>
          </Transition>

          <!-- Archived messages -->
          <TransitionGroup name="message">
            <div v-for="messageItem in store.archivedMessageHistory" :key="messageItem.key ?? messageItem.sentAt"
              :class="{
                SamuelLM: messageItem.sentBy === 'SamuelLM',
                User: messageItem.sentBy !== 'SamuelLM',
                'chat-message': true,
              }"
              :aria-label="`${messageItem.sentBy === 'SamuelLM' ? 'SamuelLM' : 'You'} said: ${messageItem.message}`">
              <div class="message-header">
                <div class="message-header-left">
                  <v-icon v-if="messageItem.sentBy === 'SamuelLM'" size="13" class="message-avatar">mdi-robot</v-icon>
                  <span
                    :class="{ message_sent_by_SamuelLM: messageItem.sentBy === 'SamuelLM', message_sent_by_User: messageItem.sentBy !== 'SamuelLM' }">
                    {{ messageItem.sentBy }}
                  </span>
                </div>
                <span :class="{ message_time_for_User: messageItem.sentBy !== 'SamuelLM', 'message-time': true }">
                  {{ formatDateTime(messageItem.sentAt) }}
                </span>
              </div>
              <div class="message-bubble-wrapper">
                <div class="message-text">{{ messageItem.message }}</div>
                <button v-if="messageItem.sentBy === 'SamuelLM'" class="copy-btn" @click="copyMessage(messageItem)"
                  :aria-label="'Copy message'">
                  <v-icon size="12">{{ copiedKey === (messageItem.key ?? messageItem.sentAt) ? 'mdi-check' :
                    'mdi-content-copy' }}</v-icon>
                </button>
              </div>
            </div>
          </TransitionGroup>

          <!-- Token limit warning -->
          <div v-if="store.archivedMessageHistory.length > 1" class="my-2 py-2">
            <v-divider :thickness="3" color="error" />
            <p class="text-body-2 text-error py-1">
              Conversation size limit reached. New messages will be part of a new conversation.
            </p>
            <v-divider :thickness="3" color="error" />
          </div>

          <!-- Active messages -->
          <TransitionGroup name="message">
            <div v-for="messageItem in store.messageHistory" :key="messageItem.key ?? messageItem.sentAt" :class="{
              SamuelLM: messageItem.sentBy === 'SamuelLM',
              User: messageItem.sentBy !== 'SamuelLM',
              'chat-message': true,
            }" :aria-label="`${messageItem.sentBy === 'SamuelLM' ? 'SamuelLM' : 'You'} said: ${messageItem.message}`">
              <div class="message-header">
                <div class="message-header-left">
                  <v-icon v-if="messageItem.sentBy === 'SamuelLM'" size="13" class="message-avatar">mdi-robot</v-icon>
                  <span
                    :class="{ message_sent_by_SamuelLM: messageItem.sentBy === 'SamuelLM', message_sent_by_User: messageItem.sentBy !== 'SamuelLM' }">
                    {{ messageItem.sentBy }}
                  </span>
                </div>
                <span :class="{ message_time_for_User: messageItem.sentBy !== 'SamuelLM', 'message-time': true }">
                  {{ formatDateTime(messageItem.sentAt) }}
                </span>
              </div>
              <div class="message-bubble-wrapper">
                <div class="message-text">
                  {{ messageItem.message }}<span v-if="messageItem.isStreaming" class="streaming-cursor">▋</span>
                </div>
                <button v-if="messageItem.sentBy === 'SamuelLM' && !messageItem.isStreaming" class="copy-btn"
                  @click="copyMessage(messageItem)" aria-label="Copy message">
                  <v-icon size="12">{{ copiedKey === (messageItem.key ?? messageItem.sentAt) ? 'mdi-check' :
                    'mdi-content-copy' }}</v-icon>
                </button>
              </div>
            </div>
          </TransitionGroup>
        </div>

        <!-- Scroll to bottom button -->
        <Transition name="scroll-btn">
          <button v-if="showScrollBtn" class="scroll-to-bottom" @click="scrollToBottom" aria-label="Scroll to bottom">
            <v-icon size="16">mdi-chevron-down</v-icon>
          </button>
        </Transition>

        <!-- ── Input area ── -->
        <div class="chat-input-area">
          <div class="input-row">
            <span class="input-caret">&gt;_</span>
            <v-textarea v-model="store.message" placeholder="Enter command..." :rows="2" class="chat-input"
              @keydown.enter.exact.prevent="sendMessage" no-resize aria-label="Type a message to SamuelLM" />
          </div>
          <div class="input-actions">
            <span class="input-hint">shift+enter for newline</span>
            <v-btn v-if="!store.isLoading && !store.isStreaming" @click="sendMessage" class="send-btn"
              aria-label="Send message" variant="tonal">
              SEND <v-icon size="14" class="ml-1">mdi-send</v-icon>
            </v-btn>
            <v-progress-circular v-else-if="store.isLoading" indeterminate color="#8BE9FD" size="22"
              aria-label="Waiting for response" />
            <v-btn v-else-if="store.isStreaming" @click="store.cancelStream()" class="stop-btn"
              aria-label="Cancel response" variant="tonal" size="small">
              STOP <v-icon size="14" class="ml-1">mdi-stop</v-icon>
            </v-btn>
          </div>
        </div>

      </div>
    </div>
  </Transition>

  <!-- ── Collapsed: desktop side tab ──────────────────────────── -->
  <Transition name="chat-fab">
    <button v-if="!store.isOpen" class="chatbox-fab d-none d-sm-flex" @click="store.isOpen = true"
      aria-label="Open chat with SamuelLM">
      <div class="fab-glow-border" />
      <div class="fab-scanlines" />
      <div class="fab-content">
        <v-icon class="fab-arrow" size="26">mdi-chevron-left</v-icon>
        <div class="fab-status">
          <span class="fab-status-dot" />
          <div class="fab-status-text">
            <span v-for="(char, i) in 'ONLINE'.split('')" :key="i" class="fab-char">{{ char }}</span>
          </div>
        </div>
        <v-icon size="22" style="color:#8BE9FD;opacity:0.9;">mdi-robot</v-icon>
        <div class="fab-label">
          <span v-for="(char, i) in 'SAMUELLM'.split('')" :key="i" class="fab-char">{{ char }}</span>
        </div>
        <span class="fab-cursor">▋</span>
        <div class="fab-tooltip">Chat with SamuelLM</div>
      </div>
    </button>
  </Transition>

  <!-- ── Collapsed: mobile FAB ──────────────────────────────── -->
  <Transition name="chat-fab">
    <button v-if="!store.isOpen" class="chatbox-mobile-fab d-flex d-sm-none" @click="store.isOpen = true"
      aria-label="Open chat with SamuelLM">
      <div class="mobile-fab-glow-border" />
      <div class="mobile-fab-scanlines" />
      <div class="mobile-fab-content">
        <span class="mobile-fab-prompt">&gt;_</span>
        <span class="mobile-fab-status-dot" />
      </div>
      <div class="mobile-fab-tooltip">Chat with SamuelLM</div>
    </button>
  </Transition>
</template>

<style>
/* ══════════════════════════════════════════════════════════
   CHAT PANEL WRAPPER
══════════════════════════════════════════════════════════ */
.chat-panel-wrapper {
  height: 100%;
  width: 100%;
  display: flex;
  position: relative;
}

.chat-panel-wrapper.is-expanded {
  position: fixed !important;
  inset: 0 !important;
  z-index: 2000;
  height: 100dvh !important;
}

/* Faint scanline texture over entire panel */
.panel-scanlines {
  position: absolute;
  inset: 0;
  background: repeating-linear-gradient(0deg,
      transparent,
      transparent 3px,
      rgba(0, 0, 0, 0.06) 3px,
      rgba(0, 0, 0, 0.06) 4px);
  pointer-events: none;
  z-index: 1;
}

/* ══════════════════════════════════════════════════════════
   PANEL TRANSITIONS
══════════════════════════════════════════════════════════ */
@media (min-width: 600px) {
  .chat-panel-enter-active {
    transition: transform 0.28s ease, opacity 0.28s ease;
  }

  .chat-panel-leave-active {
    transition: transform 0.22s ease, opacity 0.18s ease;
    position: absolute;
    right: 0;
    top: 0;
    height: 100%;
    z-index: 10;
  }

  .chat-panel-enter-from,
  .chat-panel-leave-to {
    transform: translateX(100%);
    opacity: 0;
  }
}

@media (max-width: 599px) {
  .chat-panel-enter-active {
    transition: transform 0.28s ease, opacity 0.28s ease;
  }

  .chat-panel-leave-active {
    transition: transform 0.15s ease, opacity 0.12s ease;
  }

  .chat-panel-enter-from,
  .chat-panel-leave-to {
    transform: translateY(100%);
    opacity: 0;
  }
}

/* ══════════════════════════════════════════════════════════
   CHATBOX CONTAINER
══════════════════════════════════════════════════════════ */
.chatbox-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  background: linear-gradient(180deg, #00141a 0%, #001414 60%, #001010 100%);
  font-family: 'Raleway', sans-serif;
  position: relative;
  z-index: 2;
  overflow: hidden;
}

/* ══════════════════════════════════════════════════════════
   TERMINAL TITLE BAR
══════════════════════════════════════════════════════════ */
.chat-titlebar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.6rem 0.75rem;
  background: linear-gradient(90deg, #001e1e 0%, #002828 50%, #001e1e 100%);
  border-bottom: 1px solid rgba(139, 233, 253, 0.2);
  flex-shrink: 0;
  position: relative;
}

.chat-titlebar::after {
  content: '';
  position: absolute;
  bottom: -1px;
  left: 10%;
  right: 10%;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgba(139, 233, 253, 0.4), transparent);
}

.titlebar-left {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.titlebar-status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #50FA7B;
  flex-shrink: 0;
  animation: dot-pulse 2s ease-in-out infinite;
}

.titlebar-label {
  font-family: 'Courier New', monospace;
  font-size: 0.78rem;
  font-weight: 700;
  color: #8BE9FD;
  letter-spacing: 0.08em;
}

.titlebar-version {
  color: rgba(139, 233, 253, 0.4);
  font-weight: 400;
}

.titlebar-controls {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.titlebar-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 4px;
  border: 1px solid rgba(139, 233, 253, 0.15);
  background: rgba(139, 233, 253, 0.04);
  color: rgba(139, 233, 253, 0.5);
  cursor: pointer;
  transition: all 0.15s ease;
}

.titlebar-btn:hover {
  background: rgba(139, 233, 253, 0.1);
  color: #8BE9FD;
  border-color: rgba(139, 233, 253, 0.35);
}

.titlebar-btn--close:hover {
  background: rgba(255, 85, 85, 0.15);
  color: #FF5555;
  border-color: rgba(255, 85, 85, 0.3);
}

/* ══════════════════════════════════════════════════════════
   CHAT HISTORY
══════════════════════════════════════════════════════════ */
#chat-history {
  flex-grow: 1;
  overflow-y: auto;
  padding: 1rem 0.75rem;
  height: 0;
  /* force flex child to scroll */
  scrollbar-width: thin;
  scrollbar-color: rgba(139, 233, 253, 0.2) transparent;
}

#chat-history::-webkit-scrollbar {
  width: 4px;
}

#chat-history::-webkit-scrollbar-track {
  background: transparent;
}

#chat-history::-webkit-scrollbar-thumb {
  background: rgba(139, 233, 253, 0.2);
  border-radius: 2px;
}

/* ══════════════════════════════════════════════════════════
   STARTER PROMPTS
══════════════════════════════════════════════════════════ */
.starter-prompts {
  padding: 1rem 0 1.5rem;
}

.starter-heading {
  font-family: 'Courier New', monospace;
  font-size: 0.78rem;
  color: rgba(139, 233, 253, 0.55);
  margin-bottom: 0.75rem;
}

.prompt-caret {
  color: #50FA7B;
  margin-right: 0.25rem;
}

.prompt-grid {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.prompt-chip {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.45rem 0.75rem;
  background: rgba(139, 233, 253, 0.04);
  border: 1px solid rgba(139, 233, 253, 0.15);
  border-radius: 4px;
  font-family: 'Raleway', sans-serif;
  font-size: 0.78rem;
  color: rgba(139, 233, 253, 0.7);
  cursor: pointer;
  text-align: left;
  transition: all 0.15s ease;
}

.prompt-chip:hover {
  background: rgba(139, 233, 253, 0.09);
  border-color: rgba(139, 233, 253, 0.35);
  color: #8BE9FD;
  transform: translateX(3px);
}

.prompt-bracket {
  color: rgba(139, 233, 253, 0.35);
  font-family: 'Courier New', monospace;
  font-size: 0.85rem;
}

.prompts-fade-enter-active,
.prompts-fade-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.prompts-fade-enter-from,
.prompts-fade-leave-to {
  opacity: 0;
  transform: translateY(8px);
}

/* ══════════════════════════════════════════════════════════
   MESSAGES
══════════════════════════════════════════════════════════ */
.chat-message {
  margin-bottom: 1rem;
  font-size: 0.8rem;
  max-width: 75%;
  width: fit-content;
}

.message-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.2rem;
  margin-left: 0.2rem;
  width: 100%;
}

.message-header-left {
  display: flex;
  align-items: center;
  gap: 0.3rem;
}

.message-avatar {
  color: #8BE9FD !important;
  opacity: 0.7;
}

.message_sent_by_SamuelLM {
  font-family: 'Courier New', monospace;
  font-weight: 700;
  font-size: 0.75rem;
  color: #8BE9FD;
  letter-spacing: 0.04em;
}

.message_sent_by_User {
  display: none;
}

.message-time {
  color: rgba(255, 255, 255, 0.3);
  font-family: 'Courier New', monospace;
  font-size: 0.72rem;
}

.message_time_for_User {
  margin-left: auto;
}

.message-bubble-wrapper {
  display: flex;
  align-items: flex-end;
  gap: 0.35rem;
}

.message-text {
  padding: 0.65rem 0.85rem;
  width: fit-content;
  max-width: 100%;
  border-radius: 6px;
  line-height: 1.55;
}

.SamuelLM {
  margin-right: auto;
  min-width: 200px;
}

.SamuelLM .message-header {
  min-width: 200px;
}

.SamuelLM .message-text {
  background: rgba(139, 233, 253, 0.06);
  border: 1px solid rgba(139, 233, 253, 0.12);
  border-left: 2px solid rgba(139, 233, 253, 0.4);
  color: #e0f2f2;
  border-radius: 0 6px 6px 6px;
}

.User {
  margin-left: auto;
}

.User .message-text {
  background: linear-gradient(135deg, #007f7f 0%, #00acac 100%);
  color: white;
  border-radius: 6px 0 6px 6px;
  box-shadow: 0 2px 12px rgba(0, 172, 172, 0.25);
}

/* Copy button */
.copy-btn {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: 4px;
  border: 1px solid rgba(139, 233, 253, 0.12);
  background: transparent;
  color: rgba(139, 233, 253, 0.3);
  cursor: pointer;
  opacity: 0;
  transition: all 0.15s ease;
}

.chat-message:hover .copy-btn {
  opacity: 1;
}

.copy-btn:hover {
  background: rgba(139, 233, 253, 0.08);
  color: #8BE9FD;
  border-color: rgba(139, 233, 253, 0.3);
}

/* ── Message entrance ── */
.message-enter-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.message-enter-from {
  opacity: 0;
  transform: translateY(10px);
}

.message-leave-active {
  display: none;
}

/* ══════════════════════════════════════════════════════════
   SCROLL TO BOTTOM
══════════════════════════════════════════════════════════ */
.scroll-to-bottom {
  position: absolute;
  bottom: 90px;
  right: 0.75rem;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: rgba(0, 30, 30, 0.9);
  border: 1px solid rgba(139, 233, 253, 0.3);
  color: #8BE9FD;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10;
  transition: all 0.15s ease;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.4);
}

.scroll-to-bottom:hover {
  background: rgba(0, 50, 50, 0.95);
  border-color: #8BE9FD;
  box-shadow: 0 0 10px rgba(139, 233, 253, 0.2);
}

.scroll-btn-enter-active,
.scroll-btn-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.scroll-btn-enter-from,
.scroll-btn-leave-to {
  opacity: 0;
  transform: translateY(6px);
}

/* ══════════════════════════════════════════════════════════
   INPUT AREA
══════════════════════════════════════════════════════════ */
.chat-input-area {
  flex-shrink: 0;
  border-top: 1px solid rgba(139, 233, 253, 0.1);
  padding: 0.6rem 0.75rem;
  background: rgba(0, 10, 10, 0.5);
}

.input-row {
  display: flex;
  align-items: flex-start;
  gap: 0.4rem;
}

.input-caret {
  font-family: 'Courier New', monospace;
  font-size: 0.85rem;
  color: #50FA7B;
  padding-top: 0.7rem;
  flex-shrink: 0;
  animation: caret-blink 2s ease-in-out infinite;
}

@keyframes caret-blink {

  0%,
  80%,
  100% {
    opacity: 1;
  }

  90% {
    opacity: 0.3;
  }
}

.chat-input {
  flex: 1;
  resize: none !important;
}

/* Style the Vuetify textarea to match terminal aesthetic */
.chat-input :deep(.v-field) {
  background: transparent !important;
  border: none !important;
  box-shadow: none !important;
}

.chat-input :deep(.v-field__outline) {
  display: none !important;
}

.chat-input :deep(textarea) {
  font-family: 'Raleway', sans-serif !important;
  font-size: 0.82rem !important;
  color: rgba(224, 242, 242, 0.9) !important;
  caret-color: #50FA7B !important;
}

.chat-input :deep(textarea::placeholder) {
  color: rgba(139, 233, 253, 0.25) !important;
  font-style: italic;
}

.input-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 0.15rem;
}

.input-hint {
  font-family: 'Courier New', monospace;
  font-size: 0.62rem;
  color: rgba(139, 233, 253, 0.65);
  letter-spacing: 0.03em;
}

.send-btn {
  font-family: 'Courier New', monospace !important;
  font-size: 0.72rem !important;
  letter-spacing: 0.08em !important;
  color: #8BE9FD !important;
  border: 1px solid rgba(139, 233, 253, 0.25) !important;
  background: rgba(139, 233, 253, 0.05) !important;
  min-width: unset !important;
  padding: 0 0.75rem !important;
  height: 30px !important;
  transition: all 0.15s ease !important;
}

.send-btn:hover {
  background: rgba(139, 233, 253, 0.12) !important;
  border-color: rgba(139, 233, 253, 0.5) !important;
  box-shadow: 0 0 10px rgba(139, 233, 253, 0.15) !important;
}

.stop-btn {
  font-family: 'Courier New', monospace !important;
  font-size: 0.72rem !important;
  letter-spacing: 0.08em !important;
  color: #FF5555 !important;
  border: 1px solid rgba(255, 85, 85, 0.25) !important;
  background: rgba(255, 85, 85, 0.05) !important;
  min-width: unset !important;
  padding: 0 0.75rem !important;
  height: 30px !important;
}

/* ══════════════════════════════════════════════════════════
   STREAMING CURSOR
══════════════════════════════════════════════════════════ */
@keyframes blink {

  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0;
  }
}

.streaming-cursor {
  display: inline-block;
  animation: blink 0.8s step-end infinite;
  color: #8BE9FD;
  margin-left: 1px;
}

/* ══════════════════════════════════════════════════════════
   DESKTOP CHATBOX SIZE
══════════════════════════════════════════════════════════ */
@media (min-width: 600px) {
  .chatbox-container {
    width: 100%;
    max-width: 100%;
    min-width: 280px;
  }

  .is-expanded .chatbox-container {
    width: 100%;
    max-width: 100%;
  }
}

/* ══════════════════════════════════════════════════════════
   DESKTOP FAB
══════════════════════════════════════════════════════════ */
@media (min-width: 600px) {
  .chatbox-fab {
    position: relative;
    height: 100dvh !important;
    width: 4.5rem;
    background: linear-gradient(180deg, #001010 0%, #002828 45%, #001010 100%);
    border: none;
    border-left: 1px solid rgba(139, 233, 253, 0.12);
    cursor: pointer;
    overflow: hidden;
    transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1), box-shadow 0.3s ease;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    padding: 0;
  }

  .chatbox-fab:hover {
    width: 5.2rem;
    box-shadow: -8px 0 40px rgba(139, 233, 253, 0.18), inset 0 0 60px rgba(0, 172, 172, 0.06);
  }

  .fab-glow-border {
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 2px;
    background: linear-gradient(180deg, transparent 0%, #00acac 25%, #8BE9FD 50%, #00acac 75%, transparent 100%);
    animation: glow-pulse 3s ease-in-out infinite;
  }

  @keyframes glow-pulse {

    0%,
    100% {
      opacity: 0.35;
      filter: blur(0px);
    }

    50% {
      opacity: 1;
      filter: blur(1.5px);
    }
  }

  .fab-scanlines {
    position: absolute;
    inset: 0;
    background: repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(0, 0, 0, 0.1) 2px, rgba(0, 0, 0, 0.1) 4px);
    pointer-events: none;
    z-index: 0;
  }

  .fab-content {
    position: relative;
    z-index: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 1.1rem;
  }

  .fab-status {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.35rem;
  }

  .fab-status-dot {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: #50FA7B;
    animation: dot-pulse 2s ease-in-out infinite;
  }

  @keyframes dot-pulse {

    0%,
    100% {
      box-shadow: 0 0 4px 1px rgba(80, 250, 123, 0.5);
    }

    50% {
      box-shadow: 0 0 12px 3px rgba(80, 250, 123, 0.8);
    }
  }

  .fab-status-text {
    display: flex;
    flex-direction: column;
    align-items: center;
    font-family: 'Courier New', monospace;
    font-size: 0.75rem;
    font-weight: 700;
    color: #50FA7B;
    gap: 0.15rem;
  }

  .fab-label {
    display: flex;
    flex-direction: column;
    align-items: center;
    font-family: 'Patua One', serif;
    font-size: 0.9rem;
    color: rgba(139, 233, 253, 0.65);
    transition: color 0.2s ease;
    gap: 0.1rem;
  }

  .chatbox-fab:hover .fab-label {
    color: #8BE9FD;
  }

  .fab-char {
    line-height: 1;
    display: block;
  }

  .fab-cursor {
    font-size: 0.75rem;
    color: #8BE9FD;
    opacity: 0.7;
    animation: blink-cur 1s step-end infinite;
  }

  @keyframes blink-cur {

    0%,
    100% {
      opacity: 0.7;
    }

    50% {
      opacity: 0;
    }
  }

  .fab-arrow {
    color: #8BE9FD !important;
    filter: drop-shadow(0 0 6px rgba(139, 233, 253, 0.9));
    animation: arrow-bounce 1.4s ease-in-out infinite;
  }

  @keyframes arrow-bounce {

    0%,
    100% {
      transform: translateX(0px);
      opacity: 0.6;
      filter: drop-shadow(0 0 4px rgba(139, 233, 253, 0.5));
    }

    50% {
      transform: translateX(-6px);
      opacity: 1;
      filter: drop-shadow(0 0 12px rgba(139, 233, 253, 1));
    }
  }

  .fab-tooltip {
    position: absolute;
    right: calc(100% + 10px);
    top: 50%;
    transform: translateY(-50%) translateX(8px);
    background: #001414;
    border: 1px solid rgba(139, 233, 253, 0.3);
    color: #8BE9FD;
    font-family: 'Raleway', sans-serif;
    font-size: 0.72rem;
    font-weight: 600;
    white-space: nowrap;
    padding: 0.4rem 0.75rem;
    border-radius: 6px;
    pointer-events: none;
    opacity: 0;
    transition: opacity 0.2s ease, transform 0.2s ease;
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.4);
  }

  .chatbox-fab:hover .fab-tooltip {
    opacity: 1;
    transform: translateY(-50%) translateX(0);
  }
}

/* ══════════════════════════════════════════════════════════
   FAB TRANSITION
══════════════════════════════════════════════════════════ */
.chat-fab-enter-active,
.chat-fab-leave-active {
  transition: transform 0.2s ease, opacity 0.2s ease;
}

.chat-fab-enter-from,
.chat-fab-leave-to {
  transform: scale(0.7);
  opacity: 0;
}

/* ══════════════════════════════════════════════════════════
   MOBILE FAB
══════════════════════════════════════════════════════════ */
.chatbox-mobile-fab {
  position: fixed !important;
  bottom: 1.5rem;
  right: 1.5rem;
  z-index: 999;
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background: linear-gradient(135deg, #001414 0%, #002020 100%);
  border: 1px solid rgba(139, 233, 253, 0.3);
  cursor: pointer;
  overflow: hidden;
  box-shadow:
    0 0 0 1px rgba(139, 233, 253, 0.08),
    0 4px 20px rgba(0, 0, 0, 0.5),
    0 0 20px rgba(139, 233, 253, 0.1);
  transition: all 0.2s ease;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  gap: 2px;
}

.chatbox-mobile-fab:hover {
  box-shadow:
    0 0 0 1px rgba(139, 233, 253, 0.2),
    0 4px 24px rgba(0, 0, 0, 0.5),
    0 0 30px rgba(139, 233, 253, 0.2);
  border-color: rgba(139, 233, 253, 0.5);
  transform: scale(1.05);
}

.mobile-fab-glow-border {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  background: conic-gradient(from 0deg, transparent 0%, #8BE9FD 25%, transparent 50%, #00acac 75%, transparent 100%);
  opacity: 0.15;
  animation: spin-glow 4s linear infinite;
}

@keyframes spin-glow {
  from {
    transform: rotate(0deg);
  }

  to {
    transform: rotate(360deg);
  }
}

.mobile-fab-scanlines {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  background: repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(0, 0, 0, 0.08) 2px, rgba(0, 0, 0, 0.08) 4px);
  pointer-events: none;
}

.mobile-fab-content {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.mobile-fab-prompt {
  font-family: 'Courier New', monospace;
  font-size: 1rem;
  font-weight: 700;
  color: #8BE9FD;
  filter: drop-shadow(0 0 6px rgba(139, 233, 253, 0.8));
  line-height: 1;
}

.mobile-fab-status-dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: #50FA7B;
  animation: dot-pulse 2s ease-in-out infinite;
}

.mobile-fab-tooltip {
  position: absolute;
  right: calc(100% + 10px);
  top: 50%;
  transform: translateY(-50%) translateX(8px);
  background: #001414;
  border: 1px solid rgba(139, 233, 253, 0.3);
  color: #8BE9FD;
  font-family: 'Raleway', sans-serif;
  font-size: 0.72rem;
  font-weight: 600;
  white-space: nowrap;
  padding: 0.35rem 0.65rem;
  border-radius: 6px;
  pointer-events: none;
  opacity: 0;
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.chatbox-mobile-fab:hover .mobile-fab-tooltip {
  opacity: 1;
  transform: translateY(-50%) translateX(0);
}

/* ══════════════════════════════════════════════════════════
   MOBILE FULL SCREEN
══════════════════════════════════════════════════════════ */
@media (max-width: 599px) {
  #chatbox {
    position: fixed;
    top: 0;
    bottom: 0;
    right: 0;
    left: 0;
    width: 100vw !important;
    height: 100dvh !important;
    z-index: 1000;
  }
}

.titlebar-btn:has(.mdi-delete-outline):hover {
  background: rgba(255, 85, 85, 0.15);
  color: #FF5555;
  border-color: rgba(255, 85, 85, 0.3);
}
</style>