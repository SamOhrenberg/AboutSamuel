<script setup>
import { ref, watch, nextTick } from 'vue'
import { useChatStore } from '@/stores/chatStore' // Import Pinia store
import { formatDateTime } from '@/utilities/dateUtils'

const store = useChatStore()
const chatContainer = ref(null)

// Watch for new messages and scroll to bottom
watch(
  () => store.messageHistory.length,
  async () => {
    await nextTick() // Wait for DOM update
    if (chatContainer.value) {
      chatContainer.value.scrollTop = chatContainer.value.scrollHeight
    }
  }
)


</script>

<template>
  <v-card
    v-if="store.isOpen"
    class="chatbox-container h-100 d-flex flex-column pr-3 px-3 rounded-0"
    id="chatbox"
    color="surface"
  >
    <v-card-title class="d-flex justify-space-between align-center">
      Talk to me
      <v-btn icon @click="store.isOpen = false">
        <v-icon>mdi-close</v-icon>
      </v-btn>
    </v-card-title>

    <v-card-text id="chat-history" class="flex-grow-1" ref="chatContainer">
      <div
        v-for="messageItem in store.archivedMessageHistory"
        :key="messageItem"
        class="chat-message"
      >
        <div class="message-header">
          <span>{{ messageItem.sentBy }}</span>
          <span class="message-time">{{ formatDateTime(messageItem.sentAt) }}</span>
        </div>
        <div class="message-text">{{ messageItem.message }}</div>
      </div>

      <div v-if="store.archivedMessageHistory.length > 0" class="my-2 py-2">
        <v-divider :thickness="3" color="error"></v-divider>
        <p class="text-body-2 text-error">
          Conversation size limit reached. New messages will be part of a new conversation.
        </p>
        <v-divider :thickness="3" color="error"></v-divider>
      </div>

      <div v-for="messageItem in store.messageHistory" :key="messageItem" class="chat-message">
        <div class="message-header">
          <span>{{ messageItem.sentBy }}</span>
          <span class="message-time">{{ formatDateTime(messageItem.sentAt) }}</span>
        </div>
        <div class="message-text">{{ messageItem.message }}</div>
      </div>
    </v-card-text>

    <v-divider></v-divider>

    <v-card-actions class="d-flex flex-row card-action">
      <v-text-field
        v-model="store.message"
        label="Type a message..."
        dense
        hide-details
        color="primary"
        class="chat-input"
        @keyup.enter="store.sendMessage"
      ></v-text-field>
      <v-btn @click="store.sendMessage" color="primary" variant="tonal" v-if="!store.isLoading"
        >SEND</v-btn
      >
      <v-progress-circular indeterminate v-else />
    </v-card-actions>
  </v-card>

  <!-- Floating Chat Button -->
  <v-btn v-if="!store.isOpen" class="chatbox-fab rounded-0" @click="store.isOpen = true">
    <v-icon size="x-large">mdi-chat</v-icon>
  </v-btn>
</template>

<style>
#chat-history {
  flex-grow: 1;
  overflow-y: auto;
  padding: 10px;
  height: calc(100% - 100px);
}
.card-action{
  padding: 1rem 0.5rem !important;
}
.chat-message {
  margin-bottom: 1rem;
  font-size: 0.8rem;
}

.message-header {
  display: flex;
  justify-content: space-between;
  font-size: 0.9rem;
  font-weight: bold;
  font-style: italic;
  border-bottom: 1px solid rgba(0, 0, 0, 0.75);
  margin-bottom: .5rem;
}

.message-text {
  margin-left: 10px;
}
.chat-input {
  flex: 1;
}

#chatbox{
font-family: "Raleway";
}
@media (min-width: 780px) {
  .chatbox-fab {
    width: 2rem;
    height: 100vh !important;
    background-color: #1976d2;
    color: white;
  }

  .chatbox-container {
    width: 33vw;
    max-width: 500px;
    transition: all 0.25s;
  }
}

@media (max-width: 780px) {
  .chatbox-fab {
    position: fixed;
    bottom: 0;
    right: 0;
    left: 0;
    height: 4rem !important;
    background-color: #1976d2;
    color: white;
  }

  .chatbox-container {
    position: fixed;
    bottom: 0;
    right: 0;
    left: 0;
    top: 0;
    width: 100vw !important;
    height: 100vh !important;
  }
}


</style>
