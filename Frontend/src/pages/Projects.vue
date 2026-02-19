<template>
  <v-container class="py-8" max-width="1200">
    <!-- Header -->
    <div class="mb-8">
      <h1 class="text-h3 font-weight-bold mb-2" style="font-family: 'Raleway', sans-serif">
        Projects
      </h1>
      <p class="text-body-1 text-medium-emphasis">
        A selection of the work I'm most proud of — from enterprise integrations to personal builds.
      </p>
    </div>

    <!-- Tech filter chips -->
    <div v-if="!store.loading && store.projects.length" class="mb-6">
      <v-chip
        class="mr-2 mb-2"
        :color="selectedTag === null ? 'secondary' : undefined"
        :variant="selectedTag === null ? 'flat' : 'outlined'"
        size="small"
        clickable
        @click="selectedTag = null"
      >
        All
      </v-chip>
      <v-chip
        v-for="tag in allTags"
        :key="tag"
        class="mr-2 mb-2"
        :color="selectedTag === tag ? 'secondary' : undefined"
        :variant="selectedTag === tag ? 'flat' : 'outlined'"
        size="small"
        clickable
        @click="selectedTag = selectedTag === tag ? null : tag"
      >
        {{ tag }}
      </v-chip>
    </div>

    <!-- Loading state -->
    <div v-if="store.loading" class="d-flex justify-center align-center py-16">
      <v-progress-circular indeterminate color="secondary" size="48" />
    </div>

    <!-- Error state -->
    <v-alert v-else-if="store.error" type="error" variant="tonal" class="mb-6">
      {{ store.error }}
    </v-alert>

    <!-- Projects grid -->
    <v-row v-else>
      <v-col
        v-for="project in filteredProjects"
        :key="project.projectId"
        cols="12"
        md="6"
        lg="4"
      >
        <v-card
          height="100%"
          class="d-flex flex-column project-card"
          :class="{ 'featured-card': project.isFeatured }"
          elevation="2"
          rounded="lg"
        >
          <!-- Featured badge -->
          <v-chip
            v-if="project.isFeatured"
            color="yellow"
            size="x-small"
            class="featured-badge ma-3"
            style="position: absolute; top: 0; right: 0; z-index: 1"
          >
            Featured
          </v-chip>

          <v-card-item class="pt-5">
            <v-card-title class="text-wrap" style="font-family: 'Raleway', sans-serif; line-height: 1.3">
              {{ project.title }}
            </v-card-title>
            <v-card-subtitle class="mt-1">
              {{ project.employer }}
              <span v-if="project.startYear" class="text-medium-emphasis">
                · {{ project.startYear }}{{ project.endYear ? '–' + project.endYear : '–Present' }}
              </span>
            </v-card-subtitle>
          </v-card-item>

          <v-card-text class="flex-grow-1">
            <p class="text-body-2 mb-4">{{ project.summary }}</p>

            <!-- Tech stack chips -->
            <div>
              <v-chip
                v-for="tech in project.techStack"
                :key="tech"
                size="x-small"
                variant="tonal"
                color="secondary"
                class="mr-1 mb-1"
              >
                {{ tech }}
              </v-chip>
            </div>
          </v-card-text>

          <!-- Detail expand -->
          <v-card-actions v-if="project.detail">
            <v-btn
              variant="text"
              color="secondary"
              size="small"
              :append-icon="expandedId === project.projectId ? 'mdi-chevron-up' : 'mdi-chevron-down'"
              @click="toggle(project.projectId)"
            >
              {{ expandedId === project.projectId ? 'Less detail' : 'More detail' }}
            </v-btn>
          </v-card-actions>

          <v-expand-transition>
            <div v-if="expandedId === project.projectId && project.detail">
              <v-divider />
              <v-card-text>
                <p class="text-body-2">{{ project.detail }}</p>
              </v-card-text>
            </div>
          </v-expand-transition>
        </v-card>
      </v-col>

      <!-- Empty state after filtering -->
      <v-col v-if="filteredProjects.length === 0" cols="12">
        <div class="text-center py-12 text-medium-emphasis">
          <v-icon size="48" class="mb-4">mdi-filter-off-outline</v-icon>
          <p>No projects match that filter. <v-btn variant="text" color="secondary" @click="selectedTag = null">Clear filter</v-btn></p>
        </div>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useProjectStore } from '@/stores/projectStore'

const store = useProjectStore()
const expandedId = ref(null)
const selectedTag = ref(null)

onMounted(() => {
  if (!store.projects.length) {
    store.fetchProjects()
  }
})

function toggle(id) {
  expandedId.value = expandedId.value === id ? null : id
}

const allTags = computed(() => {
  const tags = new Set()
  store.projects.forEach((p) => p.techStack?.forEach((t) => tags.add(t)))
  return [...tags].sort()
})

const filteredProjects = computed(() => {
  if (!selectedTag.value) return store.projects
  return store.projects.filter((p) => p.techStack?.includes(selectedTag.value))
})
</script>

<style scoped>
.project-card {
  transition: transform 0.15s ease, box-shadow 0.15s ease;
  position: relative;
}

.project-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.15) !important;
}

.featured-card {
  border: 1px solid rgb(var(--v-theme-secondary));
}
</style>