<script setup>
import { ref, watch, nextTick } from 'vue'
import { useChatStore } from '@/stores/chatStore' // Import Pinia store
import { formatDateTime } from '@/utilities/dateUtils'
import { useRouter } from 'vue-router'

const router = useRouter()
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

async function sendMessage() {
  let redirectToPage = await store.sendMessage()

  if (redirectToPage) {
    router.push(`/${redirectToPage}`)
  }
}
</script>

<template>
  <v-card
    v-if="store.isOpen"
    class="chatbox-container fill-height d-flex flex-column pr-3 px-3 rounded-0"
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
        :class="{
          SamuelLM: messageItem.sentBy === 'SamuelLM',
          User: messageItem.sentBy !== 'SamuelLM',
          'chat-message': true,
        }"
      >
        <div class="message-header">
          <span
            :class="{
              message_sent_by_SamuelLM: messageItem.sentBy === 'SamuelLM',
              message_sent_by_User: messageItem.sentBy !== 'SamuelLM',
            }"
          >
            {{ messageItem.sentBy }}</span
          >
          <span
            :class="{
              message_time_for_User: messageItem.sentBy !== 'SamuelLM',
              'message-time': true,
            }"
            >{{ formatDateTime(messageItem.sentAt) }}</span
          >
        </div>
        <div class="message-text">{{ messageItem.message }}</div>
      </div>

      <div v-if="store.archivedMessageHistory.length > 1" class="my-2 py-2">
        <v-divider :thickness="3" color="error"></v-divider>
        <p class="text-body-2 text-error">
          Conversation size limit reached. New messages will be part of a new conversation.
        </p>
        <v-divider :thickness="3" color="error"></v-divider>
      </div>

      <div
        v-for="messageItem in store.messageHistory"
        :key="messageItem"
        :class="{
          SamuelLM: messageItem.sentBy === 'SamuelLM',
          User: messageItem.sentBy !== 'SamuelLM',
          'chat-message': true,
        }"
      >
        <div class="message-header">
          <span
            :class="{
              message_sent_by_SamuelLM: messageItem.sentBy === 'SamuelLM',
              message_sent_by_User: messageItem.sentBy !== 'SamuelLM',
            }"
          >
            {{ messageItem.sentBy }}</span
          >
          <span
            :class="{
              message_time_for_User: messageItem.sentBy !== 'SamuelLM',
              'message-time': true,
            }"
            >{{ formatDateTime(messageItem.sentAt) }}</span
          >
        </div>
        <div class="message-text">{{ messageItem.message }}</div>
      </div>
    </v-card-text>

    <v-divider></v-divider>

    <v-card-actions class="d-flex flex-row card-action">
      <v-textarea
        v-model="store.message"
        placeholder="Type a message..."
        :rows="2"
        class="chat-input"
        @keyup.enter="sendMessage"
        no-resize="true"
      >
      </v-textarea>
      <v-btn @click="sendMessage" color="primary" variant="tonal" v-if="!store.isLoading"
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
}
.message_sent_by_User {
  display: none;
}

.message-time {
  color: grey;
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
}
.chat-input {
  flex: 1;
  resize: none !important;
}

#chatbox {
  font-family: 'Raleway';
  height: 100% !important;
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

.samuelLM {
  color: red;
  margin-right: auto;
}

.User {
  margin-left: auto;
}

.message-text {
  border-radius: 10px;
}
.SamuelLM .message-text {
  margin-right: auto;
  background-color: rgb(198, 198, 198);
}

.User .message-text {
  margin-left: auto;
  background-color: #00acac;
  color: white;
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

  /* .v-input__details{

    grid-area: control-start !important;
  }

  #input-4-messages{
    grid-area: control-start !important;


  } */

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
