<script setup>
import AboutSamuelPanel from '@/components/ExpansionPanels/AboutSamuelPanel.vue'
import ProjectsPanel from '@/components/ExpansionPanels/ProjectsPanel.vue'
import WorkHistoryPanel from '@/components/ExpansionPanels/WorkHistoryPanel.vue'
import EducationPanel from '@/components/ExpansionPanels/EducationPanel.vue'

import { ref } from 'vue'

const panel = ref(['workHistory', 'education', 'projects', 'aboutSamuel'])
</script>

<template>
  <div id="home-view" class="pa-2 pa-lg-5">
    <header class="d-flex flex-column flex-lg-row align-space-between">
      <img
        alt="Samuel Ohrenberg's headshot"
        class="rounded-circle border-dashed border-xl border-surface align-self-center mb-5 mr-lg-5"
        src="@/assets/photo.jpg"
        width="300"
        height="300"
      />

      <div id="personal-details">
        <h1 class="text-h2 text-center">Samuel Ohrenberg</h1>
        <p class="font-weight-light font-italic text-center text-subtitle-1 mb-2">
          Experienced Software Engineer, specializing in databases, API development, API
          integrations, and enterprise systems.
        </p>
        <hr class="mb-2" />
        <h2>Welcome to my portfolio!</h2>
        <p class="mb-2">
          Here you will find a more detailed record of my professional history and knowledge than
          what my resume can provide. I hope that, after taking some time to explore, you will be
          receive a more thorough understanding of who I am as a professional and the wide, varied
          skillset I have acquired over my nearly ten years in the industry.
        </p>
        <p class="mb-2">
          If you have any questions (or just like playing around with cool things), please feel free
          to speak to SamuelLM - my personal AI assistant. It should be able to tell you a great
          deal about my experience and knowledge as well as provide a preliminary answer to many
          questions you may have for me.
        </p>
      </div>
    </header>
    <v-expansion-panels
      class="my-4"
      variant="accordion"
      theme="dark"
      ripple="true"
      multiple
      v-model="panel"
    >
      <AboutSamuelPanel />
      <ProjectsPanel />
      <WorkHistoryPanel />
      <EducationPanel />
    </v-expansion-panels>
  </div>
</template>
