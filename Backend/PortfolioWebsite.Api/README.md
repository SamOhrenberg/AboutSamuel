# PortfolioWebsite.Api

The ASP.NET Core API behind aboutsamuel.com. It owns the database and everything the frontend talks to. For chat it's mostly a proxy now, since the actual LLM work moved to the [agent service](../AgentService/README.md).

.NET 10, EF Core 10, Npgsql, pgvector, Azure OpenAI, Serilog.

## Setup

`appsettings.json` is gitignored. Copy `appsettings.template.json` to `appsettings.json` and fill it in. On Railway the same settings come from environment variables, using `__` instead of `:` (so `AzureOpenAI:ApiKey` becomes `AzureOpenAI__ApiKey`, and `AllowedOrigins__0`, `AllowedOrigins__1`, etc. for the array).

| Setting | What it's for |
|---|---|
| `ConnectionStrings:DefaultConnection` | Postgres. On Railway it's `${{Postgres.DATABASE_URL}}` |
| `AllowedOrigins` | CORS. Needs `http://localhost:3000` locally |
| `AzureOpenAI:Endpoint` | The resource URL, like `https://<resource>.openai.azure.com/`. **Not** the `/openai/v1` one Foundry shows you, the client adds its own path |
| `AzureOpenAI:ApiKey` | Key from the Foundry portal |
| `AzureOpenAI:EmbeddingDeployment` | `text-embedding-3-small`. Has to match what the agent service uses or the vectors won't line up |
| `ChatSettings:ToolUse:Model` / `ChatSettings:Question:Model` | Azure **deployment** names (`gpt-4.1-mini`). Only used by the legacy `ChatService` now |
| `AdminSettings:JwtSecret` | `openssl rand -base64 32` |
| `AdminSettings:AdminEmail` | Where the admin magic link goes |
| `AdminSettings:BaseUrl` | Frontend URL the magic link points to |
| `EmailSettings:ConnectionString` | Azure Communication Services connection string, from the **Communication Services** resource's Keys page (not the Email Communication Service one) |
| `EmailSettings:From` | `DoNotReply@aboutsamuel.com`. Has to be a MailFrom address on the verified ACS domain |
| `EmailSettings:To` | Where contact form emails go |
| `Axiom:Token` / `Axiom:Dataset` | Log shipping. Optional locally, you'll just see Axiom 401s in the console |
| `AgentService:Url` | `http://localhost:8000` locally, the `*.railway.internal` address on Railway |
| `AgentService:InternalSecret` | Shared secret with the agent service. Must match its `CSHARP_API_INTERNAL_SECRET` |

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
dotnet ef migrations add <Name>
```

from this folder.

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

## Internal endpoints

`POST /contact/internal` is for the agent service's `contact_samuel` tool. It checks the `X-Internal-Secret` header against `AgentService:InternalSecret` instead of using JWT, because it's service to service.

## Admin

Admin login is a magic link. `POST /admin/login` emails a link to `AdminSettings:AdminEmail` (rate limited to 5 an hour), and `POST /admin/verify` swaps the token for a JWT. Everything under `/admin/*` needs that JWT.

## Deploying

Railway builds this from the `Dockerfile` in the repo root, not from this folder. It listens on Railway's `$PORT` (5000 if that isn't set). Package versions worth knowing about:

- `Azure.AI.OpenAI` is pinned to 2.1.0. Newer stable versions didn't exist when I set this up
- `Microsoft.AspNetCore.Authentication.JwtBearer` has to match the target framework (10.x for net10.0)
