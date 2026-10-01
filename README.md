# AboutSamuel

This is the code behind [aboutsamuel.com](https://aboutsamuel.com), my portfolio site. It's also my playground for keeping my skills sharp, so it's a little more over-engineered than a portfolio site has any right to be.

The main attraction is SamuelLM, a chat bot that answers questions about me. It uses RAG over my own work history, projects, and notes (stored as pgvector embeddings in Postgres), so it can tell you what I've actually done instead of making things up. Mostly. That's what the adversarial testing agent is going to be for.

There's also a Skill Map page that projects all of those embeddings down to 2D with UMAP, so you can poke around and see how my experience clusters together.

## How it fits together

```
 Vue 3 + Vuetify (Frontend/)
        |
        |  HTTPS, SSE for chat
        v
 ASP.NET Core API (Backend/PortfolioWebsite.Api)  ----->  PostgreSQL + pgvector
        |                         ^                              ^
        |  /chat/stream           |  /contact/internal           |
        v                         |                              |
 Python agent service (Backend/AgentService)  -------------------+
   FastAPI + LangGraph
        |
        +----> Azure OpenAI (gpt-4.1-mini, text-embedding-3-small)
        +----> RabbitMQ (background agents)
```

- **Frontend** is the site itself. It only ever talks to the C# API.
- **C# API** owns the database, the admin panel, embeddings, the Skill Map, email, and chat logging. For chat it's a proxy now and forwards the stream from the Python service.
- **Agent service** is where the LLM work lives. SamuelLM is a LangGraph ReAct agent with tools for searching my experience, redirecting to pages, and sending me a contact request (which calls back into the C# API).

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
- Docker, for RabbitMQ
- An Azure OpenAI resource with `gpt-4.1-mini` and `text-embedding-3-small` deployed

Start things in this order, since each one depends on the one before it:

1. **Postgres and RabbitMQ.** Postgres I run as a normal Windows install. RabbitMQ runs in Docker:
   ```
   docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
   ```
   After the first time it's just `docker start rabbitmq`.
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

If everything is happy, http://localhost:8000/health shows the database and queue as healthy and the chat box on the site streams responses.

## Hosting

- The C# API, Postgres, and the agent service all run on [Railway](https://railway.com). The API builds from the `Dockerfile` in the repo root. The agent service builds from `Backend/AgentService/Dockerfile` with the Railway root directory set to `Backend/AgentService`.
- The two backend services talk over Railway's private network, so the agent service URL is a `*.railway.internal` address, not a public one.
- The frontend is a static Vite build and is hosted separately.
- Logs go to Axiom.

## Where it's at

Done:

- Moved off SQL Server and AWS Bedrock to Postgres + pgvector and Azure OpenAI, all hosted on Railway
- Skill Map (embedding space visualizer)
- SamuelLM moved into the Python agent service (works locally, not deployed to Railway yet)

Next up:

- Deploy the agent service and RabbitMQ to Railway
- **Job Fit agent.** Paste in a job description and get back a gap analysis, a tailored resume section, and a cover letter.
- **Portfolio Curator agent.** Reads chat logs in the background, finds questions SamuelLM couldn't answer, and drafts new content for me to approve.
- **Adversarial Testing agent.** Tries to trick SamuelLM into making things up about me and reports back.
