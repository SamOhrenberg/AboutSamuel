<template>
  <!-- ── Hero Section ─────────────────────────────────────── -->
  <section class="hero-section" aria-label="Introduction">
    <v-container fluid class="pa-0 hero-container">
      <v-row no-gutters align="center" justify="center" class="hero-row">

        <v-col cols="12" md="auto" class="hero-photo-col">
          <!-- fetchpriority=high: browser loads this as top priority LCP image -->
          <img
            src="@/assets/sam-wedding-02.png"
            class="glow-image hero-photo"
            :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': photoVisible }"
            alt="Samuel Ohrenberg"
            fetchpriority="high"
          />
        </v-col>

        <v-col cols="12" md="auto" class="hero-text-col">
          <!-- v-once on static text: Vue skips diffing these after first render -->
          <p
            v-once
            class="hero-greeting"
            :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': greetingVisible }"
          >
            Nice to Meet You!
          </p>
          <h1
            v-once
            class="hero-name"
            :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': nameVisible }"
          >
            I'm Samuel Ohrenberg
          </h1>
          <h2
            class="hero-tagline"
            :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': taglineVisible }"
          >
            And I'm
            <span class="hero-inline-group">
              <span>A</span><span class="cycle-text" :class="{ fade: !isVisible }">{{ currentAoran }}&nbsp;</span>
              <span class="cycle-text text-yellow" :class="{ fade: !isVisible }">{{ currentNoun }}</span>
            </span>
            From Oklahoma
          </h2>
          <p
            v-once
            class="hero-sub"
            :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': subVisible }"
          >
            I'm a passionate, solution-oriented programmer who loves solving problems. If you'd like
            to learn more about me, please speak with my chatbot, SamuelLM.
          </p>
        </v-col>

      </v-row>
    </v-container>
  </section>

  <!-- ── About Section ────────────────────────────────────── -->
  <section class="about-section" aria-label="About Samuel">
    <v-container class="about-container">
      <v-row align="center" justify="center" class="about-row">

        <v-col cols="12" sm="5" md="4" class="about-photo-col">
          <!-- loading=lazy: below the fold, defer until user scrolls -->
          <img
            src="@/assets/photo.jpg"
            class="about-photo"
            alt="Samuel Ohrenberg headshot"
            loading="lazy"
          />
        </v-col>

        <v-col cols="12" sm="7" md="6" class="about-text-col">
          <h2 v-once class="about-heading">So, Who Am I?</h2>
          <p v-once class="about-body">
            I'm a software engineer from Oklahoma passionate about building robust, scalable
            solutions while always learning and exploring new technologies and strategies. When I'm
            not coding, I enjoy diving into sci-fi books and movies, engaging in tabletop games,
            and spending quality time with my wife, our little one, and our cherished pets — a
            corgi and a jack russell. Curious for more? Check out my chatbot, SamuelLM!
          </p>
        </v-col>

      </v-row>
    </v-container>
  </section>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'

// ── Cycling noun ──────────────────────────────────────────
const aoran = ['', '', 'n']
const nouns = ['Software Engineer', 'Backend Developer', 'API Developer']
const index = ref(0)
const isVisible = ref(true)

const currentNoun = computed(() => nouns[index.value])
const currentAoran = computed(() => aoran[index.value])

onMounted(() => {
  setInterval(() => {
    isVisible.value = false
    setTimeout(() => {
      index.value = (index.value + 1) % nouns.length
      isVisible.value = true
    }, 500)
  }, 4000)
})

// ── Hero animation logic ──────────────────────────────────
const HERO_ANIMATE = import.meta.env.VITE_HERO_ANIMATE ?? 'first-load'
const FIRST_LOAD_KEY = 'hero_animated'

const shouldAnimate = computed(() => {
  if (HERO_ANIMATE === 'never') return false
  if (HERO_ANIMATE === 'always') return true
  return !sessionStorage.getItem(FIRST_LOAD_KEY)
})

const photoVisible    = ref(false)
const greetingVisible = ref(false)
const nameVisible     = ref(false)
const taglineVisible  = ref(false)
const subVisible      = ref(false)

onMounted(() => {
  if (!shouldAnimate.value) {
    photoVisible.value = greetingVisible.value = nameVisible.value =
      taglineVisible.value = subVisible.value = true
    return
  }

  sessionStorage.setItem(FIRST_LOAD_KEY, '1')

  setTimeout(() => { photoVisible.value    = true }, 100)
  setTimeout(() => { greetingVisible.value = true }, 250)
  setTimeout(() => { nameVisible.value     = true }, 380)
  setTimeout(() => { taglineVisible.value  = true }, 490)
  setTimeout(() => { subVisible.value      = true }, 590)
})
</script>

<style scoped>
/* ── Cycle animation ───────────────────────────── */
.cycle-text {
  transition: opacity 0.5s ease-in-out;
  opacity: 1;
}
.fade { opacity: 0; }

/* ── Hero entrance animation ───────────────────── */
.hero-animate {
  opacity: 0;
  transform: translateY(18px);
  transition: opacity 0.5s ease, transform 0.5s ease;
}

.hero-photo.hero-animate {
  transform: scaleX(-1) translateX(20px);
}
.hero-photo.hero-animate--visible {
  transform: scaleX(-1) translateX(0) !important;
}

.hero-animate--visible {
  opacity: 1;
  transform: translate(0, 0) !important;
}

/* ── Hero section ──────────────────────────────── */
.hero-section {
  background: #001e1e;
  width: 100%;
}

.hero-container { width: 100%; }

.hero-row { min-height: 420px; }

.hero-photo-col {
  display: flex;
  justify-content: center;
  align-items: flex-end;
}

.hero-photo {
  max-width: 27rem;
  width: 100%;
  transform: scaleX(-1);
  align-self: flex-end;
  filter: drop-shadow(0 0 15px rgba(0, 255, 255, 0.5));
}

@media (max-width: 959px) {
  .hero-photo {
    max-width: 16rem;
    padding-top: 2rem;
  }
  .hero-row {
    min-height: unset;
    padding-bottom: 2rem;
  }
}

.hero-text-col {
  padding: 3rem 2rem;
  max-width: 600px;
}

@media (max-width: 959px) {
  .hero-text-col {
    padding: 1rem 1.5rem 2rem;
    text-align: center;
  }
}

.hero-greeting {
  font-family: 'Patua One', serif;
  font-size: 1.4rem;
  color: #00acac;
  margin: 0 0 0.25rem;
}

.hero-name {
  font-family: 'Patua One', serif;
  font-size: clamp(2rem, 4vw, 3.5rem);
  font-weight: 700;
  color: #ffffff;
  margin: 0 0 0.5rem;
  line-height: 1.1;
}

.hero-tagline {
  font-family: 'Patua One', serif;
  font-size: clamp(1.2rem, 2.5vw, 2rem);
  font-weight: 400;
  color: #ffffff;
  margin: 0 0 1.5rem;
  display: flex;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 0.25rem;
}

@media (max-width: 959px) {
  .hero-tagline { justify-content: center; }
}

.hero-inline-group {
  display: inline-flex;
  min-width: 16rem;
  align-items: baseline;
}

@media (max-width: 959px) {
  .hero-inline-group { min-width: unset; }
}

.hero-sub {
  font-family: 'Raleway', sans-serif;
  font-size: 0.95rem;
  font-weight: 300;
  color: rgba(255, 255, 255, 0.8);
  line-height: 1.6;
  max-width: 480px;
  margin: 0;
}

@media (max-width: 959px) {
  .hero-sub { margin: 0 auto; }
}

/* ── About section ─────────────────────────────── */
.about-section {
  background-color: #003131;
  width: 100%;
}

.about-container {
  padding: 5rem 1.5rem;
  max-width: 1100px;
}

@media (max-width: 599px) {
  .about-container { padding: 3rem 1.5rem; }
}

.about-photo-col {
  display: flex;
  justify-content: center;
  align-items: center;
}

.about-photo {
  width: 100%;
  max-width: 22rem;
  transform: scaleX(-1);
  border: 1px solid white;
  padding: 5px;
}

@media (max-width: 599px) {
  .about-photo {
    max-width: 14rem;
    border-radius: 50%;
    border: 3px solid white;
    padding: 0;
    aspect-ratio: 1;
    object-fit: cover;
    box-shadow: 6px 6px 0 #001414;
  }
}

.about-text-col { padding: 1.5rem; }

.about-heading {
  font-family: 'Patua One', serif;
  font-size: clamp(2rem, 3.5vw, 3.5rem);
  font-weight: 600;
  color: #ffffff;
  margin: 0 0 1rem;
  line-height: 1.1;
}

.about-body {
  font-family: 'Raleway', sans-serif;
  font-size: 1rem;
  font-weight: 300;
  color: rgba(255, 255, 255, 0.85);
  line-height: 1.75;
  margin: 0;
}
</style>