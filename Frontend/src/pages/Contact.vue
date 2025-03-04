<template>
  <v-container>
    <v-card class="pa-4" max-width="500px" elevation="2">
      <v-card-title>Contact Samuel</v-card-title>
      <v-card-text>
        <p class="text-caption mb-3">
          Please fill out the following form and a contact request will be generated for Sam to
          review and use to contact you. Please include your email and any pertinent details.
        </p>
        <v-divider class="mb-5" />
        <v-form ref="form" v-model="valid">
          <v-text-field
            v-model="email"
            label="Email Address"
            :rules="emailFocused ? emailRules : []"
            @blur="emailFocused = true"
            required
            variant="outlined"
            class="mb-3"
          ></v-text-field>
          <v-textarea
            v-model="message"
            label="Message (optional)"
            variant="outlined"
            auto-grow
          ></v-textarea>
          <v-alert v-if="responseMessage" :type="errorOccurred ? 'error' : 'success'" class="mt-2">
            {{ responseMessage }}
          </v-alert>
        </v-form>
      </v-card-text>
      <v-card-actions>
        <v-btn :disabled="!valid" color="primary" @click="submitForm">Submit</v-btn>
      </v-card-actions>
    </v-card>
  </v-container>
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
      'Your contact request has been sent. Samuel will try to reach out to you shortly!'
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
