# AboutSamuel

This is the code behind [aboutsamuel.com](https://aboutsamuel.com), my portfolio site. It's also my playground for keeping my skills sharp, so it's a little more over-engineered than a portfolio site has any right to be.

The main attraction is SamuelLM, a chat bot that answers questions about me. It uses RAG over my own work history, projects, and notes (stored as pgvector embeddings in Postgres), so it can tell you what I've actually done instead of making things up. Mostly. That's what the adversarial testing agent is for: it throws trick questions at SamuelLM and grades the answers, so "mostly" is an actual number.

There's also Job Fit, where you paste a job posting and get an honest read on how I match it, with every claim linked to the project or job that backs it up.

There's also a Skill Map page that projects all of those embeddings down to 2D with UMAP, so you can poke around and see how my experience clusters together.

## How it fits together

```
 Vue 3 + Vuetify (Frontend/)
        |
        |  HTTPS, SSE for chat and Job Fit
        v
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
- **C# API** owns the database, the admin panel, embeddings, the Skill Map, email, and chat logging. For chat it's a proxy now and forwards the stream from the Python service.
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

## Hosting

- The C# API, Postgres, and the agent service all run on [Railway](https://railway.com). The API builds from the `Dockerfile` in the repo root. The agent service builds from `Backend/AgentService/Dockerfile` with the Railway root directory set to `Backend/AgentService`.
- The two backend services talk over Railway's private network, so the agent service URL is a `*.railway.internal` address, not a public one.
- The frontend is a static Vite build and is hosted separately.
- Logs go to Axiom.

## Where it's at

Done:

- Moved off SQL Server and AWS Bedrock to Postgres + pgvector and Azure OpenAI, all hosted on Railway
- Skill Map (embedding space visualizer)
- SamuelLM moved into the Python agent service
- **Job Fit agent.** Paste in a job description and get back an evidence-cited fit analysis and a cover letter, streamed step by step.
- **Adversarial Testing agent.** Tries to trick SamuelLM into making things up about me, has a judge model grade every answer, and reports back in the admin panel.
- Email moved from Mailgun to Azure Communication Services

- **Hardened SamuelLM** against what the adversarial tests caught. Its pass rate went from about 68% to 98%, mostly by giving it my complete work history so it can actually tell "I never worked there" from "that just didn't come up in the search".

Next up:

- Swap the adversarial judge to a stronger model once the quota comes through, and see if it finds anything the current judge missed

Maybe someday:

- **Recruiter inbox triage.** Watch my Gmail for recruiter emails, score them against what I'm looking for, politely decline the way-off ones, and queue the good ones for me.
- **Portfolio Curator.** Mine the chat logs for questions SamuelLM couldn't answer and draft new content. Needs actual traffic first.
