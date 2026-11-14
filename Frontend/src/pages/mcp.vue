<template>
  <div class="legal-page">
    <article class="legal-inner">
      <h1>Connect Your AI</h1>
      <p class="updated">AboutSamuel Portfolio MCP server</p>

      <p>
        Rather ask your own AI about me? This is a free, public, read-only
        <a href="https://modelcontextprotocol.io" target="_blank" rel="noopener noreferrer">MCP</a> server over my
        portfolio. Connect it to Claude, ChatGPT, or any MCP client, and your assistant can look through my
        projects, skills, and work history itself and draw its own conclusions, with a link back to this site for
        everything it cites.
      </p>

      <div class="url-box">
        <code>{{ serverUrl }}</code>
        <v-btn
          size="small"
          variant="text"
          color="secondary"
          :prepend-icon="copied ? 'mdi-check' : 'mdi-content-copy'"
          @click="copyUrl"
        >{{ copied ? 'Copied' : 'Copy' }}</v-btn>
      </div>
      <p class="note">No sign-in, no API key. Streamable HTTP transport.</p>

      <h2>Connecting</h2>
      <p><strong>Claude</strong> (claude.ai, desktop, mobile):</p>
      <ol>
        <li>Open <strong>Customize &gt; Connectors</strong> and choose <strong>Add custom connector</strong>.</li>
        <li>Name it "AboutSamuel", paste the URL above, and leave the authentication settings empty.</li>
        <li>In a chat, make sure the connector is turned on in the tools menu.</li>
      </ol>
      <p><strong>Claude Code:</strong></p>
      <pre><code>claude mcp add --transport http aboutsamuel {{ serverUrl }}</code></pre>
      <p>
        <strong>Anything else</strong> (ChatGPT, Gemini CLI, VS Code, MCP Inspector): add a remote MCP server with
        the same URL wherever that client takes one.
      </p>

      <h2>Things to ask</h2>
      <ul>
        <li>"What has Samuel built with pgvector?"</li>
        <li>"Summarize Samuel's work history and what he's focused on now."</li>
        <li>"Here's a job description. How well does Samuel fit it? Cite evidence."</li>
        <li>"Which of Samuel's projects show production AI work, and what did he actually do on them?"</li>
      </ul>

      <h2>Tools</h2>
      <p>All of them are read-only. None can change anything.</p>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Tool</th><th>What it does</th></tr></thead>
          <tbody>
            <tr v-for="t in tools" :key="t.name">
              <td><code>{{ t.name }}</code></td>
              <td>{{ t.description }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p>
        There's also an <code>evaluate_candidate_fit</code> prompt. Give it a job description and it walks your
        model through pulling out the requirements, gathering evidence with the tools above, and sorting it into
        strong, partial, and no evidence, with links. The judgement is left to your model, not mine.
      </p>

      <h2>Good to know</h2>
      <ul>
        <li>
          Everything comes from my own portfolio, written by me. Your assistant should treat it as my account of
          my work, and every result links to the page it came from so you can check.
        </li>
        <li>
          Requests are rate limited per IP. Searches use semantic search when they can and fall back to keyword
          search under heavy load, and each result says which one ran.
        </li>
        <li>
          I never see your conversation, only which tools were called. The details are in the
          <router-link to="/privacy">privacy policy</router-link>, and use is covered by the
          <router-link to="/terms">terms</router-link>.
        </li>
        <li>
          The server is <a :href="repoUrl" target="_blank" rel="noopener noreferrer">open source</a>, along with
          the reasoning for keeping it
          <a :href="adrUrl" target="_blank" rel="noopener noreferrer">read-only</a>.
        </li>
      </ul>

      <h2>Support</h2>
      <p>
        Something broken or confusing? <router-link to="/contact">Send me a message</router-link> or
        <a :href="issuesUrl" target="_blank" rel="noopener noreferrer">open an issue on GitHub</a>.
      </p>
    </article>
  </div>
</template>

<script setup>
import { ref } from 'vue'

const serverUrl = 'https://api.aboutsamuel.com/mcp'
const repoUrl = 'https://github.com/SamOhrenberg/AboutSamuel'
const issuesUrl = `${repoUrl}/issues`
const adrUrl = `${repoUrl}/blob/main/docs/adr/0001-public-mcp-read-only.md`

const tools = [
  { name: 'get_profile', description: 'Who I am, what I\'m focused on, and whether I\'m looking.' },
  { name: 'list_projects', description: 'Projects in the same order as the site, filterable by technology or year.' },
  { name: 'get_project', description: 'The full write-up for one project.' },
  { name: 'get_skills', description: 'Every technology from my project tech stacks, with where and when I used it.' },
  { name: 'get_experience', description: 'Work history with achievements and linked projects.' },
  { name: 'search_portfolio', description: 'Semantic search across projects, jobs, and my notes.' },
  { name: 'find_evidence', description: 'Takes a list of job requirements and finds the best evidence for each one.' },
]

const copied = ref(false)
async function copyUrl () {
  try {
    await navigator.clipboard.writeText(serverUrl)
    copied.value = true
    setTimeout(() => { copied.value = false }, 2000)
  } catch {
    // Clipboard blocked; the URL is still selectable.
  }
}
</script>

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
ul, ol { margin: 0 0 0.9rem; padding-left: 1.2rem; }
li { margin-bottom: 0.4rem; }
a { color: rgb(var(--v-theme-secondary)); font-weight: 600; }
a:focus-visible { outline: 2px solid rgb(var(--v-theme-secondary)); outline-offset: 2px; }
code { font-size: 0.85em; padding: 0.05rem 0.3rem; border-radius: 4px; background: rgba(var(--v-theme-on-background), .08); }
pre { margin: 0 0 0.9rem; padding: 0.7rem 0.9rem; border-radius: 6px; overflow-x: auto; background: rgba(var(--v-theme-on-background), .08); }
pre code { padding: 0; background: none; }
.url-box {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  flex-wrap: wrap;
  padding: 0.6rem 0.6rem 0.6rem 0.9rem;
  border: 1px solid rgba(var(--v-theme-secondary), .5);
  border-radius: 8px;
}
.url-box code { background: none; padding: 0; font-size: 0.95rem; word-break: break-all; }
.note { font-size: 0.8rem; color: rgba(var(--v-theme-on-background), .55); margin-top: 0.4rem; }
.table-wrap { overflow-x: auto; margin: 0 0 0.9rem; }
table { width: 100%; border-collapse: collapse; }
th, td { text-align: left; vertical-align: top; padding: 0.45rem 0.6rem; border-bottom: 1px solid rgba(var(--v-theme-on-background), .12); }
th { font-weight: 700; color: rgb(var(--v-theme-on-background)); }
td:first-child { white-space: nowrap; }
</style>
