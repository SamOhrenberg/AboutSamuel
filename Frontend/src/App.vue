<template>
  <v-app>
    <main class="d-flex flex-row h-screen">
      <div id="page-wrapper">
        <Navbar />
        <div id="content-wrapper">
          <div class="flex-grow-1 overflow-y-auto" id="main-view">
            <RouterView />
          </div>
          <div id="chat-box">
            <ChatBox />
          </div>
        </div>
      </div>
    </main>
  </v-app>
</template>

<script setup>
import { RouterView } from 'vue-router'
import ChatBox from './components/ChatBox.vue'
import Navbar from './components/Navbar.vue';


</script>

<style scoped>
/* @media (min-width: 780px) {
  #chat-box {
    height: 100vh;
  }
} */

@media (max-width: 780px) {
  #chat-box {
    z-index: 1000;
  }
}

#page-wrapper{
display: flex;
flex-direction: column;
width: 100%;
}

#content-wrapper{
  display: flex;
  overflow: hidden;
}
</style>
