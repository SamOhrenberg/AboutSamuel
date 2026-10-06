<template>
  <div class="legal-page">
    <article class="legal-inner">
      <h1>Privacy Policy</h1>
      <p class="updated">Last updated October 6, 2026</p>

      <p>
        This is my personal portfolio site. I don't sell anything, run ads, or use analytics or tracking
        services. This page explains exactly what the site does with information, because it has a couple of
        AI features that do.
      </p>

      <h2>The chat (SamuelLM) and Job Fit</h2>
      <ul>
        <li>
          When you chat with SamuelLM or paste a job description into Job Fit, the text is sent to Azure OpenAI
          (Microsoft) to generate the answer. Microsoft doesn't use it to train their models.
        </li>
        <li>
          I keep a log of chat messages and replies, and of Job Fit submissions and results, in my database. I use
          it to see what people ask and to improve the answers. It's tied to a random ID created for your browser
          tab, not to your name, email, or account. There are no accounts.
        </li>
        <li>
          Don't put anything sensitive in the chat or a job description you wouldn't want me to read.
        </li>
      </ul>

      <h2>What's stored in your browser</h2>
      <ul>
        <li>Your chat history, so it's still there when you come back. Clearing your browser's site data removes it.</li>
        <li>The random chat ID and your last Job Fit result, which last until you close the tab.</li>
      </ul>
      <p>No cookies are used for tracking.</p>

      <h2>The contact form</h2>
      <p>
        If you send me a message, your email address and message are emailed to me so I can reply. I don't add
        you to any list or share your details with anyone.
      </p>

      <h2>Logs</h2>
      <p>
        Like most websites, the servers keep technical logs (requests, errors, timings) for troubleshooting. These
        are stored with my hosting provider (Railway) and logging service (Axiom) and may include technical details
        such as your IP address.
      </p>

      <h2>Recruiter Triage (Gmail access)</h2>
      <p>
        "AboutSamuel Recruiter Triage" is a private tool connected to my own Gmail account only. No one else signs
        in to it or grants it access to their account.
      </p>
      <ul>
        <li>
          It reads incoming email in my inbox to find messages from recruiters, and pulls out details about the
          role (title, company, location, pay, employment type, tech stack).
        </li>
        <li>
          It can apply labels, create draft replies, and send replies on my behalf to recruiters who emailed me.
        </li>
        <li>
          Email content is processed by Azure OpenAI to classify the message and extract the role details. I store
          the extracted details and a short excerpt in my database. Full email bodies aren't stored outside Gmail.
        </li>
        <li>
          Data from Gmail is used only to provide this triage feature for me. It isn't sold, shared, used for
          advertising, or used to train AI models.
        </li>
      </ul>
      <p>
        The use and transfer of information received from Google APIs to any other app will adhere to the
        <a href="https://developers.google.com/terms/api-services-user-data-policy" target="_blank" rel="noopener noreferrer">Google API Services User Data Policy</a>,
        including the Limited Use requirements.
      </p>

      <h2>Deleting your data</h2>
      <p>
        If you'd like your chat or Job Fit history removed, <router-link to="/contact">send me a message</router-link>
        and tell me roughly when you used it. I'll delete it.
      </p>

      <h2>Changes</h2>
      <p>If this changes, I'll update this page and the date at the top.</p>
    </article>
  </div>
</template>

<style scoped>
.legal-page {
  min-height: 100%;
  font-family: 'Raleway', sans-serif;
  background: rgb(var(--v-theme-background));
  padding: 2.5rem 1.25rem 4rem;
}
.legal-inner {
  max-width: 720px;
  margin: 0 auto;
  color: rgba(var(--v-theme-on-background), .82);
  font-size: 0.92rem;
  line-height: 1.75;
}
h1 {
  font-family: 'Patua One', serif;
  font-size: clamp(1.6rem, 4vw, 2.1rem);
  font-weight: 400;
  color: rgb(var(--v-theme-on-background));
  margin: 0;
}
h2 {
  font-family: 'Patua One', serif;
  font-size: 1.15rem;
  font-weight: 400;
  color: rgb(var(--v-theme-on-background));
  margin: 2rem 0 0.5rem;
}
.updated { font-size: 0.78rem; color: rgba(var(--v-theme-on-background), .5); margin: 0.3rem 0 1.5rem; }
p { margin: 0 0 0.9rem; }
ul { margin: 0 0 0.9rem; padding-left: 1.2rem; }
li { margin-bottom: 0.4rem; }
a { color: rgb(var(--v-theme-secondary)); font-weight: 600; }
a:focus-visible { outline: 2px solid rgb(var(--v-theme-secondary)); outline-offset: 2px; }
</style>
