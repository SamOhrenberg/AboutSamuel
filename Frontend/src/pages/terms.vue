<template>
  <div class="legal-page">
    <article class="legal-inner">
      <h1>Terms of Service</h1>
      <p class="updated">Last updated October 6, 2026</p>

      <p>
        This is my personal portfolio site. By using it you agree to these terms. They're short, I promise.
      </p>

      <h2>The AI features can be wrong</h2>
      <p>
        SamuelLM (the chat) and Job Fit are built to only say things my portfolio supports, and I test them for
        that, but they're still AI and can make mistakes. Treat what they say as a starting point, not a
        guarantee. If something matters, <router-link to="/contact">ask me directly</router-link>. Nothing on this
        site is professional, legal, or financial advice.
      </p>

      <h2>Use it reasonably</h2>
      <p>
        Please don't try to break, overload, or misuse the site or its AI features: automated scraping, trying to
        get around rate limits, or using the chat as a free general-purpose AI. Requests are rate limited, and I
        may block traffic that looks like abuse.
      </p>

      <h2>Your content</h2>
      <p>
        You're responsible for what you type into the chat, Job Fit, or the contact form. Don't submit anything you
        don't have the right to share, or anything confidential. See the
        <router-link to="/privacy">privacy policy</router-link> for what happens to it.
      </p>

      <h2>Recruiter Triage</h2>
      <p>
        "AboutSamuel Recruiter Triage" is a private tool that only works with my own Gmail account. It isn't
        offered to anyone else.
      </p>

      <h2>No warranty</h2>
      <p>
        The site is provided as is, without warranties of any kind. I'm not liable for any loss or damage from
        using it or relying on it.
      </p>

      <h2>Changes</h2>
      <p>If these change, I'll update this page and the date at the top.</p>
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
a { color: rgb(var(--v-theme-secondary)); font-weight: 600; }
a:focus-visible { outline: 2px solid rgb(var(--v-theme-secondary)); outline-offset: 2px; }
</style>
