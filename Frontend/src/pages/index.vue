<!-- <template>
  <div id="parent">
    <div id="top-content">
      <div class="header-content">
        <header>Samuel Ohrenberg</header>
      <h1>Fullstack Developer | Software Engineer</h1>
      <h2>Okalhoma City, Oklahoma</h2>
      </div>
    </div>
    <div id="middle-content">
    </div>
    <div id="bottom-content">
      <div class="img-wrapper">
        <img src="@/assets/photo.jpg">
      </div>

      <div class="text-container">
        <span>Lorem</span>
      <span>Ipsum</span>
      <span>Dolor</span>
      </div>


    </div>
    <div id="actual-bottom-content">
      <div>
        <header>About Sam</header>
        <p>Lorem ipsum dolor sit amet consectetur adipisicing elit. Dolorum, dolor enim ducimus in aperiam laboriosam ad quae doloremque quasi vel quaerat corrupti veritatis consequuntur ipsam, ut dolores harum eaque nisi.</p>
      </div>
    </div>
  </div>
</template>

<style>
html,body{
  font-family: "Patua One", Cochin, Georgia, Times, serif, Arial, sans-serif;

}

header{

  font-size: 2.8rem;
  font-weight: bold;
}
h1{
  font-weight: 200;
  padding-bottom: .5rem;
  font-size: 1.7rem;
}
h2{
  font-weight: 100;
}
#parent{
  display: flex;
  flex-direction: column;
}

  #top-content{
    background: #003131;;
    padding: 3rem 0 14rem 0;
    text-align: center;
  }

  .text-container{
    display: flex;
  justify-content: space-evenly;
  position: relative;
  bottom: 99px;
  font-size: 3rem;
}

  #middle-content{

  }
  img{
    width: 20rem;
height: 20rem;
border-radius: 50%;
object-fit: cover;
border: 3px white solid;
box-shadow: 10px 10px #003131;
  }
  #bottom-content{
    display: flex;
  flex-direction: column;
  background: #c1d9d9;
  }

  .img-wrapper{display: flex;
align-items: center;
justify-content: center;
position: relative;
bottom: 11rem;
}

#actual-bottom-content{
  padding: 1.5rem;
  background: #3e7b7b;;
}
</style> -->

<template>
  <div id="main-div">
    <div id="top-content" class="bg-primary">
      <img src="@/assets/Adobe Express - file-3.png" class="glow-image" id="header-img" />
      <div id="top-content-panel-1">
        <h1 class="text-blue_green">Nice to Meet You!</h1>
        <Header>I'm Samuel Ohrenberg</Header>
        <h2>
          And I'm A<span class="group">
            <span class="cycle-text" :class="{ fade: !isVisible }">{{ currentAoran }}&nbsp;</span>
            <span
              class="cycle-text text-yellow noun"
              :class="{
                fade: !isVisible,
                api_dev: currentNoun === 'API Developer',
                software_engineer: currentNoun === 'Software Engineer',
                backend_dev: currentNoun === 'Backend Developer',
              }"
              >{{ currentNoun }}</span
            >
          </span>
          From Oklahoma
        </h2>
        <h3>
          I'm a passionate, solution-oriented programmer who loves solving problems. If you'd like
          to learn more about me, please speak with my chatbot, SamuelLM
        </h3>
      </div>
    </div>
    <div id="bottom-content" class="bg-background">
      <div id="content-wrapper-2">
        <div id="item-wrapper">
          <div id="image-wrapper">
            <img id="about-me-image" src="@/assets/photo.jpg" />
          </div>
          <div id="about-me-wrapper">
            <header>So, Who Am I?</header>
            <p>
              Lorem, ipsum dolor sit amet consectetur adipisicing elit. Quia facilis magni voluptate
              consectetur, voluptatum ullam rerum adipisci eligendi enim explicabo tenetur
              cupiditate, culpa aperiam in accusamus deserunt amet tempora dicta?
            </p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'

const aoran = ['', '', 'n']
const nouns = ['Software Engineer', 'Backend Developer', 'API Developer']
const index = ref(0)
const isVisible = ref(true)

const currentNoun = computed(() => nouns[index.value])
const currentAoran = computed(() => aoran[index.value])

onMounted(() => {
  setInterval(() => {
    isVisible.value = false // Start fade out

    setTimeout(() => {
      index.value = (index.value + 1) % nouns.length // Change word
      isVisible.value = true // Start fade in
    }, 500) // Delay change until mid-fade (adjust timing if needed)
  }, 4000)
})
</script>

<style>
/* Reset and global styles */
body,
html {
  font-family: 'Patua One', Cochin, Georgia, Times, serif, Arial, sans-serif;
  box-sizing: border-box;
  height: 100%;
  margin: 0; /* Remove default margins */
  padding: 0; /* Remove default paddings */
}
@keyframes fadeInOut {
  0%,
  100% {
    opacity: 0;
  }
  50% {
    opacity: 1;
  }
}

.cycle-text {
  transition: opacity 0.5s ease-in-out;
  opacity: 1;
}

.fade {
  opacity: 0;
}
#content-wrapper-2 {
  display: flex;
  width: 75%;
  margin: auto;
}

/* .api_dev {
  letter-spacing: 0.086rem;
}

.backend_dev {
  letter-spacing: -0.131rem;
}

.software_engineer {
  letter-spacing: -0.099rem;
} */

.group {
  display: inline-flex;
  min-width: 18.5rem;
}
.noun {
  margin: auto;
}
#item-wrapper {
  display: flex;
  flex-direction: row;
  gap: 3rem;
  justify-content: center;
  align-content: center;
}
#about-me-wrapper {
  display: flex;
  flex-direction: column;
}
img {
  max-width: 28rem;
  transform: scaleX(-1);
  align-self: flex-end;
}
#header-img {
  height: fit-content;
}
#main-div {
  display: flex;
  flex-direction: column;
  height: 100vh;
}
#top-content {
  display: flex;
  flex-direction: row;
  width: 100%; /* Take full width */
  height: auto;
  gap: 3rem; /* Add space between panels */
  background: #001e1e;
  position: relative;
  top: 0;
  justify-content: center;
  padding: 0 2rem;
}
header,
h1,
h2,
h3 {
  padding: 0;
  margin: 0;
}
#top-content-panel-1 {
  display: flex;
  flex-direction: column;
  justify-content: center;
  padding: 15rem 0;
}

#top-content-panel-2 {
  display: flex;
}
#bottom-content {
  width: 100%;
  flex-grow: 1; /* This makes the bottom content take up the remaining space */
  display: flex;
  flex-direction: column;
  padding: 2rem;
  background-color: #003131;
  padding: 10rem 0;
  z-index: 1;
}

/* Make both panels take equal width */
/* #top-content-panel-1,
#top-content-panel-2 {
  padding: 1rem .5rem;
} */

header {
  font-size: 4rem;
  font-weight: 600;
  padding: 0.5rem 0;
  margin-top: -15px;
}

h1 {
  margin-top: -10px;
  font-size: 3rem;
  color: #00acac;
  font-weight: 500;
}

h2 {
  font-size: 2rem;
  padding-bottom: 2rem;
}

h3 {
  font-size: 1rem;
  font-weight: 100;
}
/* Glow effect on image */
.glow-image {
  filter: drop-shadow(0 0 15px rgba(var(--v-glow), 0.7));
  position: relative;
}

#about-me-image {
  padding: 5px;
  border: 1px white solid;
}
</style>
