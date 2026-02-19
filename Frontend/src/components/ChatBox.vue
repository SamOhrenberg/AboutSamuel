<script setup>
import { ref, watch, nextTick } from 'vue'
import { useChatStore } from '@/stores/chatStore'
import { formatDateTime } from '@/utilities/dateUtils'
import { useRouter } from 'vue-router'

const router = useRouter()
const store = useChatStore()
const chatContainer = ref(null)

watch(
  () => store.messageHistory.length,
  async () => {
    await nextTick()
    if (chatContainer.value) {
      chatContainer.value.scrollTop = chatContainer.value.scrollHeight
    }
  }
)

async function sendMessage() {
  let redirectToPage = await store.sendMessage()
  if (redirectToPage) {
    router.push(`/${redirectToPage}`)
  }
}
</script>

<template>
  <!-- ── Open chat panel ─────────────────────────────────────── -->
  <Transition name="chat-panel">
    <v-card
      v-if="store.isOpen"
      class="chatbox-container fill-height d-flex flex-column pr-3 px-3 rounded-0"
      id="chatbox"
      color="#001414"
      role="region"
      aria-label="Chat with SamuelLM"
    >
      <v-card-title class="d-flex justify-space-between align-center chat-title">
        Talk to me
        <v-btn icon @click="store.isOpen = false" aria-label="Close chat" variant="text">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-card-title>

      <v-card-text
        id="chat-history"
        class="flex-grow-1"
        ref="chatContainer"
        aria-live="polite"
        aria-label="Chat message history"
      >
        <!-- Archived messages -->
        <TransitionGroup name="message">
          <div
            v-for="messageItem in store.archivedMessageHistory"
            :key="messageItem.key ?? messageItem.sentAt"
            :class="{
              SamuelLM: messageItem.sentBy === 'SamuelLM',
              User: messageItem.sentBy !== 'SamuelLM',
              'chat-message': true,
            }"
            :aria-label="`${messageItem.sentBy === 'SamuelLM' ? 'SamuelLM' : 'You'} said: ${messageItem.message}`"
          >
            <div class="message-header">
              <span :class="{ message_sent_by_SamuelLM: messageItem.sentBy === 'SamuelLM', message_sent_by_User: messageItem.sentBy !== 'SamuelLM' }">
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
          <div
            v-for="messageItem in store.messageHistory"
            :key="messageItem.key ?? messageItem.sentAt"
            :class="{
              SamuelLM: messageItem.sentBy === 'SamuelLM',
              User: messageItem.sentBy !== 'SamuelLM',
              'chat-message': true,
            }"
            :aria-label="`${messageItem.sentBy === 'SamuelLM' ? 'SamuelLM' : 'You'} said: ${messageItem.message}`"
          >
            <div class="message-header">
              <span :class="{ message_sent_by_SamuelLM: messageItem.sentBy === 'SamuelLM', message_sent_by_User: messageItem.sentBy !== 'SamuelLM' }">
                {{ messageItem.sentBy }}
              </span>
              <span :class="{ message_time_for_User: messageItem.sentBy !== 'SamuelLM', 'message-time': true }">
                {{ formatDateTime(messageItem.sentAt) }}
              </span>
            </div>
            <div class="message-text">{{ messageItem.message }}</div>
          </div>
        </TransitionGroup>
      </v-card-text>

      <v-divider />

      <v-card-actions class="d-flex flex-row card-action">
        <v-textarea
          v-model="store.message"
          placeholder="Type a message..."
          :rows="2"
          class="chat-input"
          @keyup.enter="sendMessage"
          no-resize
          aria-label="Type a message to SamuelLM"
        />
        <v-btn
          v-if="!store.isLoading"
          @click="sendMessage"
          color="secondary"
          variant="tonal"
          aria-label="Send message"
        >
          SEND
        </v-btn>
        <v-progress-circular
          v-else
          indeterminate
          color="secondary"
          aria-label="Sending message, please wait"
        />
      </v-card-actions>
    </v-card>
  </Transition>

  <!-- ── Collapsed: desktop side tab ────────────────────────── -->
  <Transition name="chat-fab">
    <v-btn
      v-if="!store.isOpen"
      class="chatbox-fab d-none d-sm-flex rounded-0"
      @click="store.isOpen = true"
      aria-label="Open chat with SamuelLM"
      color="#1976d2"
    >
      <v-icon size="x-large">mdi-chat</v-icon>
    </v-btn>
  </Transition>

  <!-- ── Collapsed: mobile FAB ──────────────────────────────── -->
  <Transition name="chat-fab">
    <v-btn
      v-if="!store.isOpen"
      class="chatbox-mobile-fab d-flex d-sm-none"
      icon
      size="large"
      @click="store.isOpen = true"
      aria-label="Open chat with SamuelLM"
      color="#1976d2"
      elevation="6"
    >
      <v-icon size="large">mdi-chat</v-icon>
    </v-btn>
  </Transition>
</template>

<style>
/* ── Chat panel slide-in (desktop: from right) ── */
@media (min-width: 600px) {
  .chat-panel-enter-active,
  .chat-panel-leave-active {
    transition: transform 0.28s ease, opacity 0.28s ease;
  }
  .chat-panel-enter-from,
  .chat-panel-leave-to {
    transform: translateX(100%);
    opacity: 0;
  }
}

/* ── Chat panel slide-up (mobile: from bottom) ── */
@media (max-width: 599px) {
  .chat-panel-enter-active,
  .chat-panel-leave-active {
    transition: transform 0.28s ease, opacity 0.28s ease;
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
  display: none; /* archived messages don't need exit animation */
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

.SamuelLM { margin-right: auto; }
.SamuelLM .message-text {
  background-color: rgba(255, 255, 255, 0.12);
  color: #e0f2f2;
}

.User { margin-left: auto; }
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
    height: 100vh !important;
    width: 2rem;
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
    top: 0; bottom: 0; right: 0; left: 0;
    width: 100vw !important;
    height: 100vh !important;
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
</style>