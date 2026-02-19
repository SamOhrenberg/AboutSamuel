<template>
  <footer class="site-footer" aria-label="Site footer">
    <div class="footer-inner">

      <!-- Left: name + tagline -->
      <div class="footer-brand">
        <span class="footer-name">Samuel Ohrenberg</span>
        <span class="footer-tagline">Software Engineer · Oklahoma City</span>
      </div>

      <!-- Center: nav links -->
      <nav class="footer-nav" aria-label="Footer navigation">
        <router-link to="/" class="footer-link">Home</router-link>
        <router-link to="/resume" class="footer-link">Resume</router-link>
        <router-link to="/projects" class="footer-link">Projects</router-link>
        <router-link to="/contact" class="footer-link">Contact</router-link>
      </nav>

      <!-- Right: GitHub + copyright -->
      <div class="footer-right">
        <a
          href="https://github.com/SamOhrenberg"
          target="_blank"
          rel="noopener noreferrer"
          class="footer-github"
          aria-label="Samuel's GitHub profile"
        >
          <v-icon size="20">mdi-github</v-icon>
          <span>SamOhrenberg</span>
        </a>
        <span class="footer-copy">© {{ currentYear }} Samuel Ohrenberg</span>
      </div>

    </div>
  </footer>
</template>

<script setup>
const currentYear = new Date().getFullYear()
</script>

<style scoped>
.site-footer {
  background-color: rgb(var(--v-theme-primary));
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  padding: 1.75rem 2rem;
  font-family: 'Raleway', sans-serif;
  flex-shrink: 0;
}

.footer-inner {
  max-width: 1100px;
  margin: 0 auto;
  display: flex;
  flex-direction: row;
  align-items: center;
  justify-content: space-between;
  gap: 1.5rem;
  flex-wrap: wrap;
}

/* Brand */
.footer-brand {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.footer-name {
  font-family: 'Patua One', serif;
  font-size: 1rem;
  color: rgb(var(--v-theme-accent));
  font-weight: 600;
}

.footer-tagline {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-background), 0.45);
}

/* Nav */
.footer-nav {
  display: flex;
  gap: 1.25rem;
  flex-wrap: wrap;
  justify-content: center;
}

.footer-link {
  font-size: 0.85rem;
  color: rgba(var(--v-theme-on-background), 0.6) !important;
  text-decoration: none;
  transition: color 0.15s ease;
}

.footer-link:hover {
  color: rgb(var(--v-theme-accent)) !important;
}

.footer-link:visited {
  color: rgba(var(--v-theme-on-background), 0.6) !important;
}

/* Right */
.footer-right {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.3rem;
}

.footer-github {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.85rem;
  color: rgb(var(--v-theme-secondary)) !important;
  text-decoration: none;
  transition: opacity 0.15s ease;
}

.footer-github:hover {
  opacity: 0.8;
  text-decoration: underline;
}

.footer-copy {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-background), 0.35);
}

/* Mobile: stack vertically and center */
@media (max-width: 599px) {
  .footer-inner {
    flex-direction: column;
    align-items: center;
    text-align: center;
  }

  .footer-right {
    align-items: center;
  }

  .footer-brand {
    align-items: center;
  }
}
</style>