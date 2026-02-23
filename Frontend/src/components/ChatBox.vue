<script setup>
import { ref, watch, nextTick } from 'vue'
import { useChatStore } from '@/stores/chatStore'
import { formatDateTime } from '@/utilities/dateUtils'
import { useRouter } from 'vue-router'

const router = useRouter()
const store = useChatStore()
const chatContainer = ref(null)

function scrollToBottom() {
  nextTick(() => {
    if (chatContainer.value) {
      chatContainer.value.scrollTop = chatContainer.value.scrollHeight
    }
  })
}

// Scroll when new messages are added
watch(() => store.messageHistory.length, scrollToBottom)

// Also scroll while streaming tokens are coming in
watch(
  () => store.messageHistory.at(-1)?.message,
  scrollToBottom
)

async function sendMessage() {
  const redirectToPage = await store.sendMessage()
  if (redirectToPage) {
    router.push(`/${redirectToPage}`)
  }
}
</script>

<template>
  <!-- ── Open chat panel ─────────────────────────────────────── -->
  <Transition name="chat-panel">
    <div v-if="store.isOpen" class="chat-panel-wrapper">
      <v-card class="chatbox-container fill-height d-flex flex-column pr-3 px-3 rounded-0" id="chatbox" color="#001414"
        role="region" aria-label="Chat with SamuelLM">
        <v-card-title class="d-flex justify-space-between align-center chat-title">
          Talk to me
          <v-btn icon @click="store.isOpen = false" aria-label="Close chat" variant="text">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>

        <v-card-text id="chat-history" class="flex-grow-1" ref="chatContainer" aria-live="polite"
          aria-label="Chat message history">
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
                <span
                  :class="{ message_sent_by_SamuelLM: messageItem.sentBy === 'SamuelLM', message_sent_by_User: messageItem.sentBy !== 'SamuelLM' }">
                  {{ messageItem.sentBy }}
                </span>
                <span :class="{ message_time_for_User: messageItem.sentBy !== 'SamuelLM', 'message-time': true }">
                  {{ formatDateTime(messageItem.sentAt) }}
                </span>
              </div>
              <div class="message-text">{{ messageItem.message }}</div>
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
                <span
                  :class="{ message_sent_by_SamuelLM: messageItem.sentBy === 'SamuelLM', message_sent_by_User: messageItem.sentBy !== 'SamuelLM' }">
                  {{ messageItem.sentBy }}
                </span>
                <span :class="{ message_time_for_User: messageItem.sentBy !== 'SamuelLM', 'message-time': true }">
                  {{ formatDateTime(messageItem.sentAt) }}
                </span>
              </div>
              <div class="message-text">
                {{ messageItem.message }}<span v-if="messageItem.isStreaming" class="streaming-cursor">▋</span>
              </div>
            </div>
          </TransitionGroup>
        </v-card-text>

        <v-divider />

        <v-card-actions class="d-flex flex-row card-action">
          <v-textarea v-model="store.message" placeholder="Type a message..." :rows="2" class="chat-input"
            @keydown.enter.exact.prevent="sendMessage" no-resize aria-label="Type a message to SamuelLM" />

          <v-btn v-if="!store.isLoading && !store.isStreaming" @click="sendMessage" color="secondary" variant="tonal"
            aria-label="Send message">
            SEND
          </v-btn>

          <v-progress-circular v-else-if="store.isLoading" indeterminate color="secondary" size="24"
            aria-label="Waiting for response" />

          <v-btn v-else-if="store.isStreaming" @click="store.cancelStream()" color="error" variant="tonal" size="small"
            aria-label="Cancel response">
            STOP
          </v-btn>
        </v-card-actions>

      </v-card>
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
    <v-btn v-if="!store.isOpen" class="chatbox-mobile-fab d-flex d-sm-none" icon size="large"
      @click="store.isOpen = true" aria-label="Open chat with SamuelLM" color="#1976d2" elevation="6">
      <v-icon size="large">mdi-chat</v-icon>
    </v-btn>
  </Transition>
</template>

<style>
.chat-panel-wrapper {
  height: 100%;
  display: flex;
}

/* ── Chat panel slide-in (desktop: from right) ── */
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

/* ── Chat panel slide-up (mobile: from bottom) ── */
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

/* ── FAB pop in/out ── */
.chat-fab-enter-active,
.chat-fab-leave-active {
  transition: transform 0.2s ease, opacity 0.2s ease;
}

.chat-fab-enter-from,
.chat-fab-leave-to {
  transform: scale(0.7);
  opacity: 0;
}

/* ── Message slide-up ── */
.message-enter-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.message-enter-from {
  opacity: 0;
  transform: translateY(10px);
}

.message-leave-active {
  display: none;
  /* archived messages don't need exit animation */
}

/* ── Chat history scroll area ─── */
#chat-history {
  flex-grow: 1;
  overflow-y: auto;
  padding: 10px;
  height: calc(100% - 100px);
}

.card-action {
  padding: 1rem 0.5rem !important;
}

.chat-message {
  margin-bottom: 1rem;
  font-size: 0.8rem;
  max-width: 65%;
  width: fit-content;
}

.message-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.1rem;
  margin-left: 0.2rem;
  width: 100%;
}

.message_sent_by_SamuelLM {
  font-weight: bold;
  font-size: 0.9rem;
  color: #8BE9FD;
}

.message_sent_by_User {
  display: none;
}

.message-time {
  color: rgba(255, 255, 255, 0.4);
  font-family: Calibri, 'Trebuchet MS', sans-serif;
  font-size: 0.8rem;
}

.message_time_for_User {
  margin-left: auto;
}

.message-text {
  padding: 0.7rem;
  width: fit-content;
  max-width: 100%;
  border-radius: 10px;
}

.SamuelLM {
  margin-right: auto;
  min-width: 200px;
}

.SamuelLM .message-text {
  background-color: rgba(255, 255, 255, 0.12);
  color: #e0f2f2;
}

.User {
  margin-left: auto;
}

.User .message-text {
  background-color: #00acac;
  color: white;
}

.chat-input {
  flex: 1;
  resize: none !important;
}

#chatbox {
  font-family: 'Raleway', sans-serif;
  height: 100% !important;
}

.chat-title {
  color: #e0f2f2 !important;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  padding-bottom: 0.75rem;
}


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
    transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1),
      box-shadow 0.3s ease;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    padding: 0;
  }

  .chatbox-fab:hover {
    width: 5.2rem;
    box-shadow: -8px 0 40px rgba(139, 233, 253, 0.18),
      inset 0 0 60px rgba(0, 172, 172, 0.06);
  }

  /* Animated glow strip on left edge */
  .fab-glow-border {
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 2px;
    background: linear-gradient(180deg,
        transparent 0%,
        #00acac 25%,
        #8BE9FD 50%,
        #00acac 75%,
        transparent 100%);
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

  /* CRT scanlines */
  .fab-scanlines {
    position: absolute;
    inset: 0;
    background: repeating-linear-gradient(0deg,
        transparent,
        transparent 2px,
        rgba(0, 0, 0, 0.1) 2px,
        rgba(0, 0, 0, 0.1) 4px);
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

  /* Live status indicator */
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

  /* Blinking cursor */
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

  .chatbox-container {
    width: 33vw;
    max-width: 500px;
    min-width: 280px;
  }
}


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

.chatbox-mobile-fab {
  position: fixed !important;
  bottom: 1.5rem;
  right: 1.5rem;
  z-index: 999;
  border-radius: 50% !important;
}

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
</style>