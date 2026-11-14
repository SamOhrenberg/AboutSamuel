# AboutSamuel

This is the code behind [aboutsamuel.com](https://aboutsamuel.com), my portfolio site. It's also my playground for keeping my skills sharp, so it's a little more over-engineered than a portfolio site has any right to be.

The main attraction is SamuelLM, a chat bot that answers questions about me. It uses RAG over my own work history, projects, and notes (stored as pgvector embeddings in Postgres), so it can tell you what I've actually done instead of making things up. Mostly. That's what the adversarial testing agent is for: it throws trick questions at SamuelLM and grades the answers, so "mostly" is an actual number.

There's also Job Fit, where you paste a job posting and get an honest read on how I match it, with every claim linked to the project or job that backs it up.

There's also a Skill Map page that projects all of those embeddings down to 2D with UMAP, so you can poke around and see how my experience clusters together.

And if you'd rather have your own AI do the asking, there's a public, read-only [MCP server](#public-mcp-server). Point Claude (or any MCP client) at it and it can look through my projects and experience itself.

## How it fits together

```
 Vue 3 + Vuetify (Frontend/)          Claude, ChatGPT, any MCP client
        |                                       |
        |  HTTPS, SSE for chat and Job Fit      |  /mcp, read-only
        v                                       v
 ASP.NET Core API (Backend/PortfolioWebsite.Api)  ----->  PostgreSQL + pgvector
        |                         ^                              ^
        |  /chat, /job-fit,       |  /contact/internal           |
        |  /adversarial           |                              |
        v                         |                              |
 Python agent service (Backend/AgentService)  -------------------+
   FastAPI + LangGraph
        |
        +----> Azure OpenAI (gpt-4.1-mini, text-embedding-3-small)
```

- **Frontend** is the site itself. It only ever talks to the C# API.
- **C# API** owns the database, the admin panel, embeddings, the Skill Map, email, and chat logging. For chat it's a proxy now and forwards the stream from the Python service. It also hosts the MCP server, which reads Postgres directly and never touches the agent service.
- **Agent service** is where the LLM work lives. SamuelLM is a LangGraph ReAct agent with tools for searching my experience, redirecting to pages, and sending me a contact request (which calls back into the C# API). Job Fit and the adversarial tester are fixed LangGraph pipelines.

## Repo layout

```
Backend/
  PortfolioWebsite.Api/     ASP.NET Core API (.NET 10, EF Core, Npgsql)
  PortfolioWebsite.Common/  shared helpers
  AgentService/             Python FastAPI + LangGraph service
Frontend/                   Vue 3 + Vuetify + Vite
Dockerfile                  builds the C# API for Railway
```

Each project has its own README with the details:

- [Backend/PortfolioWebsite.Api/README.md](Backend/PortfolioWebsite.Api/README.md)
- [Backend/AgentService/README.md](Backend/AgentService/README.md)
- [Frontend/README.md](Frontend/README.md)

## Running it locally

You'll need:

- .NET 10 SDK
- Python 3.11+
- Node (whatever's current, it's Vite)
- PostgreSQL with the pgvector extension
- An Azure OpenAI resource with `gpt-4.1-mini` and `text-embedding-3-small` deployed

Start things in this order, since each one depends on the one before it:

1. **Postgres.** I run it as a normal Windows install.
2. **Agent service** on port 8000. See its README for the `.env` setup.
   ```
   cd Backend/AgentService
   .\.venv\Scripts\Activate.ps1
   uvicorn main:app --reload --port 8000
   ```
3. **C# API** on https://localhost:7276. Run the `https` profile in Visual Studio, or:
   ```
   cd Backend/PortfolioWebsite.Api
   dotnet run --launch-profile https
   ```
4. **Frontend** on http://localhost:3000.
   ```
   cd Frontend
   npm install
   npm run dev
   ```

If everything is happy, http://localhost:8000/health shows the database as healthy and the chat box on the site streams responses.

## Public MCP server

SamuelLM answers questions in my words. The MCP server skips the middleman: your AI gets the same data I do and draws its own conclusions. It's at

```
https://api.aboutsamuel.com/mcp
```

No sign-in, no API key. It's Streamable HTTP, so anything that supports remote MCP servers can use it.

**Connecting from Claude:** Customize > Connectors > Add custom connector, paste the URL above, leave the auth settings empty. Then ask it something like "What has Samuel built with pgvector?" and watch it call the tools. Other clients (ChatGPT, Gemini CLI, MCP Inspector) take the same URL wherever they add a remote server.

What it can do:

| Tool | What it's for |
|---|---|
| `get_profile` | Who I am, what I'm focused on, whether I'm looking |
| `list_projects` | Projects in the same order as the site, filterable by tech or year |
| `get_project` | The full write-up for one project |
| `get_skills` | Every technology from my project tech stacks, with where and when I used it |
| `get_experience` | Work history with achievements and linked projects |
| `search_portfolio` | Semantic search across projects, jobs and my notes |
| `find_evidence` | Takes a list of job requirements and finds the best evidence for each one |

There's also an `evaluate_candidate_fit` prompt: give it a job description and it walks the model through extracting the requirements, gathering evidence with the tools above, and splitting it into strong, partial and no evidence, with links. Unlike Job Fit, the judgement is left to your model, not mine.

Every result has a link back to the page on aboutsamuel.com, so whatever the AI tells you, you can check.

**It's read-only, on purpose, at every layer.** The tools are annotated read-only, they go through a database context that throws if anything tries to save, and that context logs into Postgres as a role that can only `SELECT` four public tables. A public server that also had write tools would be a prompt injection target, since a connected model mixes its user's instructions with whatever text it reads. The reasoning is in [docs/adr/0001-public-mcp-read-only.md](docs/adr/0001-public-mcp-read-only.md).

**It costs me almost nothing.** The only paid call is embedding the search query (never your data, never an LLM call), and those are cached, rate limited per IP, capped per day, and fall back to plain Postgres full-text search when a limit is hit. Details are in the [API README](Backend/PortfolioWebsite.Api/README.md#mcp-server).

## Hosting

- The C# API, Postgres, and the agent service all run on [Railway](https://railway.com). The API builds from the `Dockerfile` in the repo root. The agent service builds from `Backend/AgentService/Dockerfile` with the Railway root directory set to `Backend/AgentService`.
- The two backend services talk over Railway's private network, so the agent service URL is a `*.railway.internal` address, not a public one.
- The frontend is a static Vite build and is hosted separately.
- Traces, logs and metrics from both backend services go to Azure Monitor (App Insights) through OpenTelemetry. A chat shows up as one trace across the C# API, the agent service and Azure OpenAI.

## Where it's at

Done:

- Moved off SQL Server and AWS Bedrock to Postgres + pgvector and Azure OpenAI, all hosted on Railway
- Skill Map (embedding space visualizer)
- SamuelLM moved into the Python agent service
- **Job Fit agent.** Paste in a job description and get back an evidence-cited fit analysis and a cover letter, streamed step by step.
- **Adversarial Testing agent.** Tries to trick SamuelLM into making things up about me, has a judge model grade every answer, and reports back in the admin panel.
- Email moved from Mailgun to Azure Communication Services
- **Public MCP server.** Read-only tools over my portfolio for recruiters' AIs, with semantic search and a job-requirements evidence finder. Also the first real test suite (xUnit + Testcontainers) and CI for the API.

- **Hardened SamuelLM** against what the adversarial tests caught. Its pass rate went from about 68% to 98%, mostly by giving it my complete work history so it can actually tell "I never worked there" from "that just didn't come up in the search".

Next up:

- Swap the adversarial judge to a stronger model once the quota comes through, and see if it finds anything the current judge missed

Maybe someday:

- **Recruiter inbox triage.** Watch my Gmail for recruiter emails, score them against what I'm looking for, politely decline the way-off ones, and queue the good ones for me.
- **Portfolio Curator.** Mine the chat logs for questions SamuelLM couldn't answer and draft new content. Needs actual traffic first.
