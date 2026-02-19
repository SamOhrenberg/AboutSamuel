<template>
  <div class="contact-page">
    <div class="contact-inner">

      <!-- Left: intro text -->
      <div class="contact-intro">
        <h1 class="contact-title">Get In Touch</h1>
        <p class="contact-lead">
          I'm always open to new opportunities, collaborations, or just a conversation.
          Fill out the form and I'll get back to you as soon as I can.
        </p>
        <div class="contact-detail">
          <v-icon color="secondary" size="20">mdi-map-marker-outline</v-icon>
          <span>Oklahoma City, Oklahoma</span>
        </div>
        <div class="contact-detail">
          <v-icon color="secondary" size="20">mdi-github</v-icon>
          <a href="https://github.com/SamOhrenberg" target="_blank" rel="noopener noreferrer" class="contact-link">
            github.com/SamOhrenberg
          </a>
        </div>
        <p class="contact-hint">
          Prefer to chat with an AI first? Ask
          <strong class="text-secondary">SamuelLM</strong> in the panel to the right.
        </p>
      </div>

      <!-- Right: form card -->
      <v-card class="contact-card" elevation="0">
        <v-card-text class="contact-form-body">
          <v-form ref="form" v-model="valid">
            <label class="form-label" for="contact-email">Email Address</label>
            <v-text-field
              id="contact-email"
              v-model="email"
              placeholder="you@example.com"
              :rules="emailFocused ? emailRules : []"
              @blur="emailFocused = true"
              required
              variant="outlined"
              density="comfortable"
              class="mb-4"
              color="secondary"
            />

            <label class="form-label" for="contact-message">Message <span class="form-optional">(optional)</span></label>
            <v-textarea
              id="contact-message"
              v-model="message"
              placeholder="What's on your mind?"
              variant="outlined"
              density="comfortable"
              auto-grow
              :rows="4"
              color="secondary"
              class="mb-4"
            />

            <v-alert
              v-if="responseMessage"
              :type="errorOccurred ? 'error' : 'success'"
              variant="tonal"
              class="mb-4"
              rounded="lg"
            >
              {{ responseMessage }}
            </v-alert>

            <v-btn
              :disabled="!valid"
              color="secondary"
              variant="flat"
              size="large"
              block
              @click="submitForm"
              class="submit-btn"
            >
              Send Message
              <v-icon end>mdi-send</v-icon>
            </v-btn>
          </v-form>
        </v-card-text>
      </v-card>

    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { sendContactRequest } from '@/services/contactService'
import constants from '@/utilities/constants'

const email = ref('')
const message = ref('')
const responseMessage = ref('')
const errorOccurred = ref(false)
const valid = ref(false)
const form = ref(null)
const emailFocused = ref(false)

const emailRules = [
  (v) => !!v || 'Email is required',
  (v) => /.+@.+\..+/.test(v) || 'Email is not valid',
]

const submitForm = async () => {
  responseMessage.value = ''
  errorOccurred.value = false
  if (!form.value.validate()) return

  try {
    await sendContactRequest(email.value, message.value)
    responseMessage.value =
      'Your message has been sent — Samuel will be in touch soon!'
    email.value = ''
    message.value = ''
    form.value.reset()
  } catch (error) {
    console.error(error)
    errorOccurred.value = true
    responseMessage.value = constants.unknownSystemError
  }
}
</script>

<style scoped>
.contact-page {
  min-height: 100%;
  display: flex;
  align-items: flex-start;
  padding: 4rem 2rem;
  font-family: 'Raleway', sans-serif;
}

.contact-inner {
  display: flex;
  flex-direction: row;
  gap: 4rem;
  max-width: 960px;
  width: 100%;
  margin: 0 auto;
  align-items: flex-start;
}

@media (max-width: 768px) {
  .contact-inner {
    flex-direction: column;
    gap: 2rem;
  }
  .contact-page {
    padding: 2.5rem 1.25rem;
  }
}

/* ── Left intro ── */
.contact-intro {
  flex: 1;
  min-width: 0;
}

.contact-title {
  font-family: 'Patua One', serif;
  font-size: clamp(2rem, 4vw, 3rem);
  font-weight: 700;
  margin: 0 0 1rem;
  color: rgb(var(--v-theme-on-background));
  line-height: 1.1;
}

.contact-lead {
  font-size: 1rem;
  line-height: 1.7;
  color: rgba(var(--v-theme-on-background), 0.7);
  margin: 0 0 1.75rem;
}

.contact-detail {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  font-size: 0.9rem;
  color: rgba(var(--v-theme-on-background), 0.75);
  margin-bottom: 0.75rem;
}

.contact-link {
  color: rgb(var(--v-theme-secondary)) !important;
  text-decoration: none;
}
.contact-link:hover {
  text-decoration: underline;
}

.contact-hint {
  margin-top: 2rem;
  font-size: 0.85rem;
  color: rgba(var(--v-theme-on-background), 0.5);
  line-height: 1.6;
  padding: 0.75rem 1rem;
  border-left: 3px solid rgb(var(--v-theme-secondary));
  background: rgba(var(--v-theme-secondary), 0.06);
  border-radius: 0 6px 6px 0;
}

/* ── Form card ── */
.contact-card {
  flex: 1.1;
  min-width: 0;
  background-color: rgb(var(--v-theme-surface)) !important;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 14px !important;
}

.contact-form-body {
  padding: 2rem !important;
}

.form-label {
  display: block;
  font-size: 0.8rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: rgba(var(--v-theme-on-surface), 0.6);
  margin-bottom: 0.4rem;
}

.form-optional {
  font-weight: 400;
  text-transform: none;
  letter-spacing: 0;
  opacity: 0.7;
}

.submit-btn {
  font-family: 'Raleway', sans-serif;
  font-weight: 700;
  letter-spacing: 0.04em;
}
</style>