# PortfolioWebsite.Api

The ASP.NET Core API behind aboutsamuel.com. It owns the database and everything the frontend talks to. For chat it's mostly a proxy now, since the actual LLM work moved to the [agent service](../AgentService/README.md).

.NET 10, EF Core 10, Npgsql, pgvector, Azure OpenAI, Serilog.

## Setup

`appsettings.json` is gitignored. Copy `appsettings.template.json` to `appsettings.json` and fill it in. On Railway the same settings come from environment variables, using `__` instead of `:` (so `AzureOpenAI:Endpoint` becomes `AzureOpenAI__Endpoint`, and `AllowedOrigins__0`, `AllowedOrigins__1`, etc. for the array).

| Setting | What it's for |
|---|---|
| `ConnectionStrings:DefaultConnection` | Postgres. On Railway it's `${{Postgres.DATABASE_URL}}` |
| `ConnectionStrings:PublicReadOnly` | Postgres as the `mcp_readonly` role, for the [MCP server](#mcp-server). Falls back to `DefaultConnection` with a startup warning if unset |
| `Mcp:SiteBaseUrl` | Base of the links MCP results point at. Defaults to `https://aboutsamuel.com` |
| `Mcp:RateLimit:*` | Per-IP limit on `/mcp`. Defaults are fine, see [Limits](#limits) |
| `Mcp:SemanticSearch:*` | Query embedding budget for the MCP search tools. Defaults are fine, see [Search and what it costs](#search-and-what-it-costs) |
| `AllowedOrigins` | CORS. Needs `http://localhost:3000` locally |
| `AzureOpenAI:Endpoint` | `https://aboutsamuel-ai.openai.azure.com/`, from the `openAiEndpoint` output of `infra/main.bicep`. **Not** the `/openai/v1` one Foundry shows you, the client adds its own path |
| `AzureOpenAI:EmbeddingDeployment` | `embedding`. Has to match what the agent service uses or the vectors won't line up |
| `ChatSettings:ToolUse:Model` / `ChatSettings:Question:Model` | Azure **deployment** names (`chat`). Only used by the legacy `ChatService` now |
| `AdminSettings:JwtSecret` | `openssl rand -base64 32` |
| `AdminSettings:AdminEmail` | Where the admin magic link goes |
| `AdminSettings:BaseUrl` | Frontend URL the magic link points to |
| `EmailSettings:ConnectionString` | Azure Communication Services connection string, from the **Communication Services** resource's Keys page (not the Email Communication Service one) |
| `EmailSettings:From` | `DoNotReply@aboutsamuel.com`. Has to be a MailFrom address on the verified ACS domain |
| `EmailSettings:To` | Where contact form emails go |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Turns on OpenTelemetry tracing and logs to App Insights. Optional locally. Ingestion is Entra-only, so sending needs *Monitoring Metrics Publisher* on `appi-aboutsamuel`. Set on Railway by `infra/scripts/sync-railway-vars.sh` |
| `AgentService:Url` | `http://localhost:8000` locally, the `*.railway.internal` address on Railway |
| `AgentService:InternalSecret` | Shared secret with the agent service. Must match its `CSHARP_API_INTERNAL_SECRET` |

The Azure deployments are defined in `infra/main.bicepparam` and named by role (`chat`, `embedding`, `judge`), so swapping a model never changes these values. On Railway the `AzureOpenAI__*` vars are set by `infra/scripts/sync-railway-vars.sh`; don't edit them in the dashboard.

There's no Azure OpenAI key. `Services/AzureOpenAIRegistration.cs` builds one `AzureOpenAIClient` with `DefaultAzureCredential`. Locally that's your `az login` (it needs *Cognitive Services OpenAI User* on `aboutsamuel-ai`, granted in `infra/bootstrap/main.bicepparam`). If Visual Studio is signed in as a different account it gets tried first, so set `AZURE_TOKEN_CREDENTIALS=AzureCliCredential`. On Railway it's the `sp-aboutsamuel-railway` service principal, set up by `infra/scripts/set-railway-sp-credential.sh`.

## Running

Run the `https` profile in Visual Studio, or:

```
dotnet run --launch-profile https
```

That serves on https://localhost:7276. Swagger is at `/swagger`, but only in Development.

If the browser complains about the cert, run `dotnet dev-certs https --trust` once.

## Database

Postgres with the pgvector extension. The migrations add `vector(1536)` columns but don't create the extension, so on a fresh database run this once first:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

(The agent service also runs that on connect, so if it's already been pointed at the database you're covered.)

Migrations run automatically on startup, locally and on Railway. To add one:

```
dotnet ef migrations add <Name> --context SqlDbContext
```

from this folder. `--context` is required because there's a second context, `PublicPortfolioDbContext`, a read-only view of the public tables for the MCP server. It's excluded from migrations, so `SqlDbContext` still owns the whole schema.

`ConnectionStrings:PublicReadOnly` is that context's connection, logging in as the `mcp_readonly` role from `infra/sql/mcp-readonly-role.sql` (run by hand, see the comment at the top). If it's unset, it falls back to `DefaultConnection` and logs a warning at startup.

**Timezones:** write `DateTimeOffset` values as UTC (`DateTimeOffset.UtcNow` or `.ToUniversalTime()`). Npgsql rejects non-UTC offsets for `timestamptz`, which you'll only notice locally because Railway already runs in UTC.

## Embeddings

`Information`, `Projects`, and `WorkExperiences` each have an `Embedding` column. After changing content in the admin panel, regenerate them:

- `POST /admin/generate-embeddings` fills in every entry that doesn't have an embedding yet
- `POST /admin/generate-embeddings/{type}/{id}` regenerates one entry
- `POST /admin/embedding-visualization/regenerate` rebuilds the 2D UMAP projection for the Skill Map

Editing an Information or Work Experience entry clears its embedding, so the bulk endpoint picks it up. Editing a **Project** doesn't, so either regenerate that one project or it keeps its old embedding.

Chat search and the Skill Map both read these, so stale embeddings mean stale answers.

## Chat

`POST /Chat/stream` forwards the request to the agent service (`AgentServiceClient`) and streams it back to the browser as SSE. When the stream finishes it saves the conversation to the `Chats` table, which is what the admin chat log reads.

What the frontend gets:

```
data: {"token":"Hi"}
data: {"token":" there"}
data: {"redirectToPage":"projects","tokenLimitReached":false,"error":false}
data: [DONE]
```

The agent service sends its meta in snake_case nested under `meta`. `AgentServiceClient` parses that and the controller re-sends it flat and camelCase, so the frontend doesn't care which backend is doing the work. Keep it that way.

`tokenLimitReached` is always `false` right now. The agent service doesn't track it yet.

The old `ChatService` + `AzureLlmService` path is still here. `POST /Chat` (non-streaming) uses it, but nothing in the frontend calls it anymore, so it can probably go. Resume generation (`GET`/`POST /Chat/resume`) still uses `ChatService`.

## MCP server

A public, read-only [MCP](https://modelcontextprotocol.io) server at `/mcp`, so recruiters' AIs can query my portfolio directly. The tool list and how to connect are in the [root README](../../README.md#public-mcp-server). Everything lives in `Mcp/`.

```
 MCP client (Claude, ChatGPT, Inspector)
        |  POST /mcp  (Streamable HTTP, stateless, no auth)
        v
 rate limiter "Mcp" (per IP) -> 64 KB body cap -> MapMcp
        |
        v
 McpToolObserver (call-tool filter: one log line + mcp.tool.duration)
        |
        v
 PortfolioTools / PortfolioSearchTools      validate input (McpInput), shape results
        |                        |
        v                        v
 PortfolioReadService     PortfolioSearchService ---> QueryEmbeddingProvider ---> Azure OpenAI
        |                        |                    (cache, per-IP, daily cap)   (query only)
        v                        v
 PublicPortfolioDbContext  (no tracking, SaveChanges throws)
        |
        v
 Postgres as mcp_readonly  (SELECT on 4 tables, read-only sessions)
```

It's the official C# SDK (`ModelContextProtocol.AspNetCore`), registered in `McpRegistration` (`AddPublicPortfolio`, `AddPublicMcpServer`, `MapPublicMcp`). The SDK runs stateless, so there are no sessions to keep and any instance can answer any request.

### Read-only, four ways

Each layer would stop a write on its own:

1. **Tool annotations.** Every tool is `ReadOnly = true, Destructive = false`, so clients know it's safe to call without asking.
2. **No write code.** `IPortfolioReadService` and `IPortfolioSearchService` only have read methods, and the MCP layer has its own DTOs in `Mcp/Contracts`, so admin-only fields like `IsActive` and `DisplayOrder` never leak.
3. **`PublicPortfolioDbContext`.** It maps only `Projects`, `WorkExperiences`, `ProjectWorkExperience` and `Information`. It's `NoTracking`, every `SaveChanges*` throws, and it's excluded from migrations.
4. **The `mcp_readonly` role.** `infra/sql/mcp-readonly-role.sql` grants `SELECT` on those four tables and nothing else, and sets `default_transaction_read_only = on` as well. Even a raw SQL write from this connection fails in Postgres.

Inactive projects and jobs are filtered out in the service, so hiding something in the admin panel hides it here too.

Content is public, but the server still never touches the private tables (chats, admin tokens, Job Fit runs, the resume files, and so on). If a new table should be visible, it needs a mapping in the context **and** a grant in the role script. That's deliberate.

### Search and what it costs

`search_portfolio` and `find_evidence` use the same cosine search over the `Embedding` columns that the chat does, ported to C# so they don't need the agent service. The only paid call is embedding the query text, through `QueryEmbeddingProvider`:

1. `Mcp:SemanticSearch:Enabled` off means full-text only, and Azure OpenAI is never called.
2. **Cache.** Keyed by SHA-256 of the normalized query, `CacheMinutes` long (60), up to `CacheMaxEntries` (1000). Hits are free and don't count against the limits.
3. **Per IP.** `PerIpPerHour` embedding calls (300). A `find_evidence` batch is one call.
4. **Daily cap.** `DailyCap` texts per UTC day across everyone (2000). This is the real cost ceiling, and at text-embedding-3-small prices it's a fraction of a cent.
5. **Fallback.** If any of those say no, or the embedding call fails, the search runs on Postgres full-text instead (`to_tsvector` / `websearch_to_tsquery`, terms ORed, ranked by `ts_rank`). Results say which one ran in `mode`.

It's all or nothing per call, so `find_evidence` never mixes modes. The counters are in memory, which is exact as long as there's one instance.

Limits are generous because Claude.ai and ChatGPT call from their own servers, so lots of people can share one IP.

### Limits

- **Rate limit:** `Mcp:RateLimit:PermitLimit` requests per `WindowSeconds` per IP (300 per minute), counted on every request (initialize, list, call). Over the limit is a plain 429. One conversation is roughly 20 requests.
- **Body size:** 64 KB (`McpRegistration.MaxRequestBodyBytes`), sized for the biggest valid input, a 20,000 character job description. A Content-Length over it gets a 413 before the SDK reads anything. Kestrel enforces the same limit on chunked bodies.
- **Inputs:** `McpInput` checks every argument (lengths, ranges, page size up to 25, up to 15 requirements) and returns a readable tool error. The limits are also in the input schemas, but the SDK doesn't enforce those, so `McpInput` does.

### Logging and errors

- **Analytics.** `McpToolObserver` logs one line per tool call (`MCP tool call {McpTool} success {McpSuccess} in {ElapsedMs} ms`) and records the `mcp.tool.duration` histogram (seconds, tagged `tool` and `success`) on the `PortfolioWebsite.Api.Mcp` meter, which goes to App Insights. No arguments, no results, no IPs. A tool name the server doesn't have is recorded as `unknown`, so clients can't write arbitrary text into the logs.
- **Errors.** A tool's own errors (bad input, unknown slug) come back as readable tool errors. Anything unexpected comes back as the SDK's generic `An error occurred invoking '<tool>'.`, and the full exception is logged on our side.
- **Log filtering.** The SDK logs every expected client error at Error with a stack trace, and those messages repeat the client's arguments. `McpRegistration.IsSdkClientError` drops them in Serilog, and the `ModelContextProtocol` category is clamped to Warning. Real faults aren't `McpException`s, so they still get logged.

### Profile

`get_profile` reads `Mcp/profile.json` (committed, copied to the build output). Edit it and redeploy; there's no admin UI for it. A null `Availability` tells the model I haven't said, not that I'm unavailable.

### Trying it locally

Run the API (`dotnet run --launch-profile https`), then:

```
npx @modelcontextprotocol/inspector
```

In the Inspector, pick **Streamable HTTP**, use `http://localhost:5133/mcp` (the plain HTTP port, because Node doesn't trust the dev cert), and connect. Tools, the prompt, and each tool's input and output schemas are all there to poke at.

## Tests

```
dotnet test Backend/PortfolioWebsite.Api.sln
```

from the repo root. Docker has to be running: the integration tests start a `pgvector/pgvector:pg17` container with Testcontainers, boot the real API against it (startup migrations included), and apply the real `mcp-readonly-role.sql`, so the MCP tests read through the actual read-only role. Embeddings are faked, so no Azure access is needed.

- `PortfolioWebsite.Api.Tests.Unit`: the pure helpers, tool logic over fakes, the embedding provider's limits.
- `PortfolioWebsite.Api.Tests.Integration`: the MCP server end to end with a real `McpClient`, the read-only layers, rate and size limits, and a check that running every tool leaves every table's row count and row versions (`xmin`) unchanged.

Each tool's advertised contract (annotations, input and output schemas) is snapshotted in `PortfolioWebsite.Api.Tests.Integration/Snapshots/`. If you change one on purpose, run with `MCP_UPDATE_SNAPSHOTS=1` once and commit the new files.

CI (`.github/workflows/api-tests.yml`) runs the same thing on PRs and pushes to `main` that touch the API.

## Internal endpoints

`POST /contact/internal` is for the agent service's `contact_samuel` tool. It checks the `X-Internal-Secret` header against `AgentService:InternalSecret` instead of using JWT, because it's service to service.

## Admin

Admin login is a magic link. `POST /admin/login` emails a link to `AdminSettings:AdminEmail` (rate limited to 5 an hour), and `POST /admin/verify` swaps the token for a JWT. Everything under `/admin/*` needs that JWT.

## Deploying

Railway builds this from the `Dockerfile` in the repo root, not from this folder. It listens on Railway's `$PORT` (5000 if that isn't set). Package versions worth knowing about:

- `Azure.AI.OpenAI` is pinned to 2.1.0. Newer stable versions didn't exist when I set this up
- `Microsoft.AspNetCore.Authentication.JwtBearer` has to match the target framework (10.x for net10.0)
