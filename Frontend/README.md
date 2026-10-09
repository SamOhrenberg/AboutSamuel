# Frontend

The Vue side of aboutsamuel.com. Vue 3, Vuetify 3, Pinia, Vite. It started from the Vuetify scaffold, which is why the folders under `src/` have their own little READMEs.

Everything it needs comes from the [C# API](../Backend/PortfolioWebsite.Api/README.md). It never talks to the agent service directly.

## Setup

```
npm install
npm run dev
```

That runs on http://localhost:3000 (with `--host`, so you can hit it from your phone on the same network). The API has to have `http://localhost:3000` in its `AllowedOrigins` or every request fails CORS.

`npm run build` makes the production build in `dist/`.

## Environment

These live in `.env`. That file **is** committed, which is fine because everything prefixed with `VITE_` gets baked into the bundle and anyone can read it anyway. So never put a secret in there.

| Variable | What it does |
|---|---|
| `VITE_API_URL` | The C# API. `https://localhost:7276` locally |
| `VITE_HERO_ANIMATE` | Home page hero animation: `first-load` (default), `always`, or `never` |

## Layout

```
src/
  pages/        file-based routes (unplugin-vue-router), so the file name is the URL
    admin/      admin panel, behind a magic link login
  components/   auto-imported, ChatBox.vue is the chat
  stores/       Pinia. chatStore.js runs the chat
  services/     API calls. chatService.js parses the chat stream
  layouts/      default.vue wraps every page
```

## Pages

- `/` home
- `/projects` and `/work-experience` support `?id=<guid>` to scroll to and highlight one entry. The Skill Map uses that to link you over
- `/skill-map` the embedding visualizer
- `/resume` the resume. The API has the LLM build it from the database, and you can give it a job title and description to get a tailored version
- `/contact`
- `/testimonial`
- `/admin` content management, chat logs, and embedding regeneration

**If you rename or add a page**, check `VALID_PAGES` in `Backend/AgentService/tools/contact.py`. That's the list of pages SamuelLM is allowed to send people to, and it has to match the file names here.

## Chat

`chatService.js` POSTs to `/Chat/stream` and reads the response as server-sent events:

```
data: {"token":"Hi"}
data: {"redirectToPage":"projects","tokenLimitReached":false,"error":false}
data: [DONE]
```

Tokens get appended to the message as they come in. If the meta has a `redirectToPage`, `ChatBox.vue` sends the router there. For `projects` it also adds `?highlight=<the question>`.

If the whole answer shows up in one chunk (short canned responses), the store fakes the streaming client-side so it doesn't just pop in.

## Admin

`/admin/login` asks the API to email a magic link. The link goes to `/admin/verify`, which swaps the token for a JWT and keeps it in `sessionStorage`, so closing the tab logs you out.
