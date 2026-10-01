# AgentService

The Python service that does the LLM work for aboutsamuel.com. FastAPI on the outside, LangGraph on the inside.

Right now it runs SamuelLM, the chat bot on the site. The C# API calls it for every chat message and streams the answer back to the browser. Three more agents (Job Fit, Portfolio Curator, Adversarial Testing) are stubbed out and wired to RabbitMQ, but they don't do anything yet.

It only talks to the C# API, never to the browser directly.

## Layout

```
main.py                 FastAPI app. Starts the queue consumers on startup
run.py                  production entrypoint (dual-stack socket, see Gotchas)
config.py               settings, read from .env
api/
  chat.py               POST /chat/stream, POST /chat/query
  health.py             GET /health
agents/
  samuellm/
    agent.py            the LangGraph ReAct agent, stream_chat and query_chat
    prompts.py          SamuelLM's system prompt
  job_fit/              stub
  portfolio_curator/    stub
  adversarial/          stub
tools/
  retrieval.py          search_experience (pgvector search)
  contact.py            contact_samuel, get_resume, redirect_to_page, ask_clarification
database/connection.py  asyncpg pool
messaging/              RabbitMQ connection and consumers
```

## Setup

Python 3.11 locally (the Docker image is 3.12, both work).

```
py -3.11 -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

Then make a `.env` in this folder. It's gitignored, keep it that way, it has the Azure key in it.

```
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com/
AZURE_OPENAI_API_KEY=
AZURE_OPENAI_CHAT_DEPLOYMENT=gpt-4.1-mini
AZURE_OPENAI_EMBEDDING_DEPLOYMENT=text-embedding-3-small
DATABASE_URL=postgresql://<user>:<password>@localhost:5432/portfoliodb
RABBITMQ_URL=amqp://guest:guest@localhost:5672/
CSHARP_API_URL=https://localhost:7276
CSHARP_API_INTERNAL_SECRET=
```

- `AZURE_OPENAI_ENDPOINT` is the resource URL, not the `/openai/v1` one the Foundry portal shows. LangChain adds its own path.
- `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` has to be the same model the C# API used to build the embeddings, or search quietly returns garbage.
- `CSHARP_API_INTERNAL_SECRET` has to match `AgentService:InternalSecret` in the C# API.

Settings are cached, and `--reload` only watches `.py` files, so restart the server after changing `.env`.

RabbitMQ runs in Docker:

```
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

The management UI is at http://localhost:15672 (guest/guest). If RabbitMQ isn't running the service still starts, it just logs `queue_startup_failed` and chat works fine.

## Running

```
uvicorn main:app --reload --port 8000
```

- http://localhost:8000/health checks the database and RabbitMQ. It does **not** check Azure OpenAI, so a healthy health check doesn't mean chat works.
- http://localhost:8000/docs is Swagger. Handy for hitting `/chat/stream` without the frontend.

## How SamuelLM works

It's a LangGraph ReAct loop. The LLM either answers or calls a tool, the tool result goes back to the LLM, and it repeats until there's an answer. `stream_chat` uses `astream_events` and sends back two kinds of SSE events:

```
data: {"token": "..."}
data: {"meta": {"redirect_to_page": "projects", "full_response": "..."}}
```

The C# API reshapes the meta for the frontend (see its README). If you change these field names, update `AgentServiceClient.cs` too.

The tools:

- `search_experience` embeds the question and runs a cosine search across `Information`, `Projects`, and `WorkExperiences`, then merges the results.
- `redirect_to_page` sends the user to a page on the site. The valid pages are in `VALID_PAGES` in `tools/contact.py` and **must match the Vue routes** in `Frontend/src/pages`. If you add a page, add it there too.
- `get_resume` is a redirect to the resume page.
- `contact_samuel` POSTs to the C# API's `/contact/internal` with the `X-Internal-Secret` header, and the C# API sends the email.
- `ask_clarification` is for when the question is too vague.

The redirect tools don't do anything themselves. They return a sentinel string like `__REDIRECT__projects__`, and `agent.py` catches it in the `on_tool_end` event and turns it into the meta. That output is a `ToolMessage`, so read `.content`, not `str(output)`.

The LLM usually still writes a sentence after a redirect even though the prompt says not to. That's gpt-4.1-mini being gpt-4.1-mini. The redirect still works.

## Background agents

`messaging/connection.py` declares three durable queues on startup:

- `portfolio.curator.analyze`
- `portfolio.adversarial.test`
- `portfolio.job_fit.request`

`messaging/consumer.py` sends each message to its agent. All three agents are stubs that raise `NotImplementedError`, and the consumer requeues failed messages, so **anything you publish to these queues right now will retry forever.** Don't publish to them until the agent is real.

## Gotchas

- The folder is called `messaging` and not `queue` because `queue` is a Python stdlib module and naming it that breaks imports.
- pgvector: let `pgvector.asyncpg.register_vector` handle it (already done in `database/connection.py`) and pass embeddings as plain Python lists. Don't format them as `"[1.0,2.0,...]"` strings yourself.
- The Dockerfile runs `run.py`, not `uvicorn --host ::`. Railway's healthcheck comes in over IPv4 and its private network uses IPv6, and `uvicorn --host ::` only listens on IPv6 (asyncio sets `IPV6_V6ONLY`). `run.py` binds one socket that takes both. Don't "simplify" it back, the healthcheck will time out.
- `tools/contact.py` uses `verify=False` because the C# API uses a self-signed dev cert locally. On Railway it's plain HTTP on the private network, so it doesn't matter there.

## Deploying

Not on Railway yet. The plan:

- New Railway service from this repo with the root directory set to `Backend/AgentService`. It builds from the `Dockerfile` here and listens on `$PORT`.
- RabbitMQ from the [Railway template](https://railway.com/deploy/rabbitmq)
- Same env vars as `.env`, except `DATABASE_URL=${{Postgres.DATABASE_URL}}`, `RABBITMQ_URL=${{RabbitMQ.RABBITMQ_URL}}`, and `CSHARP_API_URL` pointing at the C# API's `*.railway.internal` address
- Set `AgentService__Url` and `AgentService__InternalSecret` on the C# API service
