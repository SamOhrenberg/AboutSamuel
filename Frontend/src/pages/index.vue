<template>
  <!-- ── Hero Section ─────────────────────────────────────── -->
  <section class="hero-section" aria-label="Introduction">
    <v-container fluid class="pa-0 hero-container">
      <v-row no-gutters align="center" justify="center" class="hero-row">

        <v-col cols="12" md="auto" class="hero-photo-col">
          <!-- fetchpriority=high: browser loads this as top priority LCP image -->
          <img ref="heroPhotoEl" src="@/assets/sam-wedding-02.png" class="glow-image hero-photo"
            :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': photoVisible }" alt="Samuel Ohrenberg"
            fetchpriority="high" />
        </v-col>

        <v-col cols="12" md="auto" class="hero-text-col">
          <p class="hero-greeting" :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': greetingVisible }">
            Nice to Meet You!
          </p>
          <h1 class="hero-name" :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': nameVisible }">
            I'm Samuel Ohrenberg
          </h1>
          <h2 class="hero-tagline" :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': taglineVisible }">
            And I'm
            <span class="hero-inline-group">
              <span>A</span><span class="cycle-text" :class="{ fade: !isVisible }">{{ currentAoran }}&nbsp;</span>
              <span class="cycle-text text-yellow" :class="{ fade: !isVisible }">{{ currentNoun }}</span>
            </span>
            From Oklahoma
          </h2>
          <p class="hero-sub" :class="{ 'hero-animate': shouldAnimate, 'hero-animate--visible': subVisible }">
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

        <v-col cols="12"  lg="4" class="about-photo-col"  style="order: 1">
          <img ref="aboutPhotoEl" src="@/assets/photo.jpg" class="about-photo about-animate"
            :class="{ 'about-animate--visible': aboutPhotoVisible }" alt="Samuel Ohrenberg headshot" loading="lazy" />
        </v-col>

        <v-col ref="aboutTextEl" cols="12"  lg="6" class="about-text-col about-animate about-animate--text"
          :class="{ 'about-animate--visible': aboutTextVisible }"  style="order: 2">
          <h2 class="about-heading">So, Who Am I?</h2>
          <p class="about-body">
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

const photoVisible = ref(false)
const greetingVisible = ref(false)
const nameVisible = ref(false)
const taglineVisible = ref(false)
const subVisible = ref(false)

onMounted(() => {
  if (!shouldAnimate.value) {
    photoVisible.value = greetingVisible.value = nameVisible.value =
      taglineVisible.value = subVisible.value = true
    return
  }

  sessionStorage.setItem(FIRST_LOAD_KEY, '1')

  setTimeout(() => { photoVisible.value = true }, 100)
  setTimeout(() => { greetingVisible.value = true }, 250)
  setTimeout(() => { nameVisible.value = true }, 380)
  setTimeout(() => { taglineVisible.value = true }, 490)
  setTimeout(() => { subVisible.value = true }, 590)
})

// ── About section entrance ──────────────────────────────────────────────
const aboutPhotoEl = ref(null)
const aboutTextEl = ref(null)
const aboutPhotoVisible = ref(false)
const aboutTextVisible = ref(false)

const heroPhotoEl = ref(null)

onMounted(() => {
  // About section observer
  const aboutObserver = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          if (entry.target === aboutPhotoEl.value) aboutPhotoVisible.value = true
          if (entry.target === aboutTextEl.value.$el) aboutTextVisible.value = true
          aboutObserver.unobserve(entry.target)
        }
      })
    },
    { threshold: 0.15 }
  )

  if (aboutPhotoEl.value) aboutObserver.observe(aboutPhotoEl.value)
  if (aboutTextEl.value) aboutObserver.observe(aboutTextEl.value.$el)

  // Hero photo tilt
  const photo = heroPhotoEl.value
  if (!photo || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

  const handleMouseMove = (e) => {
    const rect = photo.getBoundingClientRect()
    const cx = rect.left + rect.width / 2
    const cy = rect.top + rect.height / 2
    const dx = (e.clientX - cx) / (rect.width / 2)  // -1 to 1
    const dy = (e.clientY - cy) / (rect.height / 2)  // -1 to 1
    const rotY = dx * 6   // max 6deg
    const rotX = -dy * 4  // max 4deg
    photo.style.transform = `scaleX(-1) perspective(600px) rotateY(${rotY}deg) rotateX(${rotX}deg)`
  }

  const handleMouseLeave = () => {
    photo.style.transform = 'scaleX(-1) perspective(600px) rotateY(0deg) rotateX(0deg)'
  }

  // Tilt responds to the whole hero section, not just the photo
  const heroSection = photo.closest('section')
  heroSection?.addEventListener('mousemove', handleMouseMove, { passive: true })
  heroSection?.addEventListener('mouseleave', handleMouseLeave, { passive: true })
})
</script>

<style scoped>
/* ── Cycle animation ───────────────────────────── */
.cycle-text {
  transition: opacity 0.5s ease-in-out;
  opacity: 1;
}

.fade {
  opacity: 0;
}

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

.hero-container {
  width: 100%;
}

.hero-row {
  min-height: 420px;
  gap: 0 3rem;
}

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
  transition: transform 0.12s ease-out, filter 0.3s ease;
  will-change: transform;
}

.hero-photo:not(:hover) {
  transition: transform 0.5s cubic-bezier(0.23, 1, 0.32, 1), filter 0.3s ease;
}

@media (max-width: 1200px) {
  .hero-photo-col {
    position: relative;
    overflow: hidden;
    /* Spotlight effect behind Samuel */
    background: radial-gradient(
      ellipse 70% 50% at 50% 75%,  /* pull it up slightly */
      rgba(0, 172, 172, 0.12) 0%,
      transparent 70%
    );
  }

  .hero-text-col {
    text-align: center;
    background: linear-gradient(170deg,
        #0d4444 0%,
        #0a3d3d 50%,
        #083838 100%);
    border-top: 2px solid rgba(139, 233, 253, 0.3);
    /* Glowing top edge */
    box-shadow:
      0 -1px 0 rgba(139, 233, 253, 0.1),
      0 -8px 40px rgba(0, 0, 0, 0.5),
      inset 0 1px 0 rgba(139, 233, 253, 0.08);
    margin-top: -3rem;
    margin-left: -12px;
    margin-right: -12px;
    width: calc(100% + 24px);
    max-width: calc(100% + 24px);
    position: relative;
    z-index: 2;
    padding: 2.5rem 2rem 3rem;

    /* Subtle circuit-board-like corner accent */
    border-bottom: 1px solid rgba(139, 233, 253, 0.08);
  }

  /* Decorative cyan accent bar above "Nice to Meet You" */
  .hero-text-col::before {
    content: '';
    position: absolute;
    top: -2px;
    left: 50%;
    transform: translateX(-50%);
    width: 60px;
    height: 3px;
    border-radius: 2px;
    filter: blur(1px);
  }

  .hero-greeting {
    font-size: 1.1rem;
    letter-spacing: 0.05em;
  }

  .hero-name {
    font-size: clamp(2rem, 7vw, 3rem);
    text-shadow: 0 0 40px rgba(0, 172, 172, 0.3);
  }

  .hero-tagline {
    justify-content: center;
    font-size: clamp(1rem, 3.5vw, 1.5rem);
  }

  .hero-inline-group {
    min-width: unset;
  }

  .hero-sub {
    margin: 0 auto;
    font-size: 0.9rem;
    opacity: 0.85;
  }

  .hero-section {
    overflow: hidden;
  }

  .hero-row {
    min-height: unset;
    padding-bottom: 0;
    position: relative;
  }
}

.hero-text-col {
  padding: 3rem 2rem;
  max-width: 700px;
}

@media (max-width: 1200px) {
  .hero-text-col {
    padding: 2rem 1.5rem 2.5rem;
    text-align: center;
    background: #0a3d3d;
    border-top: 1px solid rgba(139, 233, 253, 0.15);
    margin-top: -3rem;
    margin-left: -12px;
    margin-right: -12px;
    width: calc(100% + 24px);
    max-width: calc(100% + 24px);
    position: relative;
    z-index: 2;
    box-shadow: 0 -8px 32px rgba(0, 0, 0, 0.4);
  }

  .hero-section {
    overflow: hidden;
  }
}

.hero-greeting {
  font-family: 'Patua One', serif;
  font-size: 1.4rem;
  color: #00acac;
  margin: 0 0 0.25rem;
  padding-top: 1rem;
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

@media (max-width: 1200px) {
  .hero-tagline {
    justify-content: center;
  }
}

.hero-inline-group {
  display: inline-flex;
  min-width: 16rem;
  align-items: baseline;
}

@media (max-width: 1200px) {
  .hero-inline-group {
    min-width: unset;
  }
}

.hero-sub {
  font-family: 'Raleway', sans-serif;
  font-size: 0.95rem;
  font-weight: 300;
  color: rgba(255, 255, 255, 0.8);
  line-height: 1.6;
  max-width: 480px;
  margin: 0;
  padding-bottom: 1rem;
}

@media (max-width: 1200px) {
  .hero-sub {
    margin: 0 auto;
  }
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
  .about-container {
    padding: 3rem 1.5rem;
  }
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
  transition: box-shadow 0.6s ease, border-color 0.6s ease;
}

.about-animate--visible.about-photo {
  box-shadow: 0 0 0 1px rgba(0, 172, 172, 0.3), 0 12px 40px rgba(0, 0, 0, 0.4);
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

  .about-animate--visible.about-photo {
    box-shadow: 6px 6px 0 #001414, 0 0 0 1px rgba(0, 172, 172, 0.3);
  }
}

@media (max-width: 900px) {
  .about-photo-col {
    order: 1;
  }

  .about-text-col {
    order: 2;
    text-align: center;
  }

  .about-heading::after {
    left: 50%;
    transform: translateX(-50%);
  }
}

.about-text-col {
  padding: 1.5rem;
}

.about-heading {
  font-family: 'Patua One', serif;
  font-size: clamp(2rem, 3.5vw, 3.5rem);
  font-weight: 600;
  color: #ffffff;
  margin: 0 0 1rem;
  line-height: 1.1;
  position: relative;
  display: inline-block;
}

.about-heading::after {
  content: '';
  position: absolute;
  bottom: -4px;
  left: 0;
  width: 0;
  height: 3px;
  background: #00acac;
  border-radius: 2px;
  transition: width 0.5s 0.35s ease;
}

.about-animate--visible .about-heading::after {
  width: 60%;
}

@media (prefers-reduced-motion: reduce) {
  .about-heading::after {
    width: 60%;
    transition: none;
  }
}

.about-body {
  font-family: 'Raleway', sans-serif;
  font-size: 1rem;
  font-weight: 300;
  color: rgba(255, 255, 255, 0.85);
  line-height: 1.75;
  margin: 0;
}


/* ── About entrance ──────────────────────────────────────────────────── */
.about-animate {
  opacity: 0;
  transform: translateY(28px);
  transition: opacity 0.6s ease, transform 0.6s ease;
}

/* Photo slides in from the left instead */
.about-animate:not(.about-animate--text) {
  transform: translateX(-24px);
}

.about-animate--visible {
  opacity: 1;
  transform: translate(0, 0);
}

/* Stagger: text waits slightly longer than the photo */
.about-animate--text {
  transition-delay: 0.12s;
}

@media (prefers-reduced-motion: reduce) {
  .about-animate {
    opacity: 1;
    transform: none;
    transition: none;
  }
}
</style>