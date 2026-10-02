# AgentService

The Python service that does the LLM work for aboutsamuel.com. FastAPI on the outside, LangGraph on the inside.

It runs two agents:

- **SamuelLM**, the chat bot on the site. The C# API calls it for every chat message and streams the answer back to the browser.
- **Job Fit**, which takes a job description and streams back an honest, evidence-cited fit analysis and a cover letter.

Two more (Portfolio Curator, Adversarial Testing) are stubbed out and wired to RabbitMQ, but they don't do anything yet.

It only talks to the C# API, never to the browser directly.

## Layout

```
main.py                 FastAPI app. Starts the queue consumers on startup
run.py                  production entrypoint (dual-stack socket, see Gotchas)
log_config.py           structlog setup, ships logs to Axiom when configured
config.py               settings, read from .env
api/
  chat.py               POST /chat/stream, POST /chat/query
  job_fit.py            POST /job-fit/stream
  health.py             GET /health
agents/
  samuellm/
    agent.py            the LangGraph ReAct agent, stream_chat and query_chat
    prompts.py          SamuelLM's system prompt
  job_fit/
    agent.py            the four nodes and the graph. Runnable on its own, see below
    state.py            Pydantic models for structured output, and the graph state
    prompts.py          one prompt per LLM step
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
- `AXIOM_TOKEN` and `AXIOM_DATASET` are optional. Leave them out locally and logs only go to the console. On Railway they ship every structlog event to Axiom with `service: agent-service` on it.

Axiom shipping runs on a background thread so logging never blocks the event loop (which would stall token streaming). If Axiom is down, that batch gets dropped and you'll see `axiom_ingest_failed` on stderr. uvicorn's own access log lines (`POST /chat/stream ...`) aren't structlog, so they only show up in Railway.

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

## How Job Fit works

Unlike SamuelLM, it's a fixed pipeline, not a ReAct loop. The steps are always the same, so the code decides the order and the LLM does one focused job per step. That keeps cost and time predictable (3 LLM calls plus one embedding call, about 15 to 25 seconds) and makes each step testable.

```
extract_requirements  -> not a job description? stop
retrieve_evidence     -> no LLM
assess_fit
write_cover_letter
```

- **extract_requirements** pulls up to 12 requirements out of the posting with strict structured output, and marks each one must have or nice to have. The posting is untrusted input, so it's wrapped in tags and treated as data. Prompt injection comes back as "not a job description".
- **retrieve_evidence** embeds every requirement in one batched call and runs a pgvector search for each. It's tuned for recall, not precision: `text-embedding-3-small` scores bunch up between about 0.2 and 0.55, so a strict cutoff throws away real matches. The 0.2 floor only removes noise.
- **assess_fit** rates each requirement strong, partial, or no evidence, citing evidence by short aliases (E1, E2) because LLMs mistype GUIDs. It also gets the full career timeline from `WorkExperiences`, with the span worked out in code, because LLMs can't add up date ranges. The code then checks the result: one verdict per requirement, citations must be evidence that was actually retrieved for that requirement, a match with no valid citation gets downgraded, and padded citations get trimmed for technical skills. The overall rating is calculated in code, not by the LLM (must haves count double).
- **write_cover_letter** only sees the strong and partial matches and their evidence, so it can't claim anything that wasn't verified. It's the only step that streams tokens.

"No evidence" means the portfolio doesn't show it, not that I don't have it. Keeping the portfolio content current matters more here than anywhere else on the site.

To run the whole graph from a terminal without the API:

```
python -m agents.job_fit.agent path/to/job_description.txt
```

`POST /job-fit/stream` takes `{"jobDescription": "..."}` (12,000 characters max) and streams SSE events: `step` when each node starts, `requirements`, `evidenceCount`, `assessment`, `token` for the cover letter, `error` if it stopped early, and always `done` last.

## Background agents

`messaging/connection.py` declares two durable queues on startup:

- `portfolio.curator.analyze`
- `portfolio.adversarial.test`

(Job Fit used to have a queue too, but it runs over HTTP since a visitor is waiting on the result.)

`messaging/consumer.py` sends each message to its agent. Both agents are stubs that raise `NotImplementedError`, and the consumer requeues failed messages, so **anything you publish to these queues right now will retry forever.** Don't publish to them until the agent is real.

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
