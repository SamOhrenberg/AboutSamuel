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
  },
)
</script>

<template>
  <v-card v-if="store.isOpen" class="chatbox-container">
    <v-card-title class="d-flex justify-space-between align-center">
      Talk to me
      <v-btn icon @click="store.isOpen = false">
        <v-icon>mdi-close</v-icon>
      </v-btn>
    </v-card-title>

    <v-card-text style="height: 300px">
      <div id="chat-history" ref="chatContainer">
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
      </div>
    </v-card-text>

    <v-divider></v-divider>

    <v-card-actions class="chat-controls" v-if="!store.isLoading">
      <v-text-field
        v-model="store.message"
        label="Type a message..."
        dense
        hide-details
        class="chat-input"
        @keyup.enter="store.sendMessage"
      ></v-text-field>
      <v-btn @click="store.sendMessage" color="primary">SEND</v-btn>
    </v-card-actions>

    <v-card-actions v-else>
      <v-progress-circular indeterminate />
    </v-card-actions>
  </v-card>

  <!-- Floating Chat Button -->
  <v-btn v-if="!store.isOpen" class="chatbox-fab" icon @click="store.isOpen = true">
    <v-icon size="x-large">mdi-chat</v-icon>
  </v-btn>
</template>

<style scoped>
.chatbox-container {
  position: fixed;
  width: 400px;
  height: 100%;
  display: flex;
  flex-direction: column;
  right: 0;
  bottom: 0;
}

@media (max-width: 600px) {
  .chatbox-container {
    position: fixed;
    left: 0;
    top: 0;
    display: flex;
    flex-direction: column;
    width: 100vw;
    height: 100vh;
  }
}

.chat-content {
  display: flex;
  flex-direction: column;
  flex-grow: 1;
  height: 325px;
  overflow: auto;
}

.chatbox-fab {
  position: fixed;
  bottom: 20px;
  right: 20px;
  background-color: #1976d2;
  color: white;
}

#chat-history {
  flex-grow: 1;
  overflow-y: auto;
  padding: 10px;

  height: 100%;
  overflow: auto;
}

.chat-message {
  margin-bottom: 1rem;
  font-size: 0.8rem;
  /* background-color: rgb(189, 233, 248);
  padding: 6px; */
  /* border: 1px solid rgb(114, 142, 151);
  border-radius: 3px;
  box-shadow: rgba(114, 142, 151, 0.8) 0px 2px 2px; */
}

.message-header {
  display: flex;
  justify-content: space-between;
  font-size: 0.9rem;
  font-weight: bold;
  font-style: italic;
  border-bottom: 1px solid rgba(0, 0, 0, 0.75);
}

.message-text {
  margin-left: 10px;
}

.chat-controls {
  display: flex;
  padding: 10px;
  background: white; /* Ensures the input stays visible */
}

.chat-input {
  flex: 1;
}
</style>
