# 1. The public MCP server is read-only, and admin tools get their own server

- **Status:** Accepted
- **Date:** 2026-11-13

## Context

I wanted recruiters to be able to point their own AI (Claude, ChatGPT, whatever they use) at my portfolio and ask it questions, instead of only having SamuelLM, which answers in my words. MCP is the obvious way to do that: one URL at `/mcp`, and any client that supports remote servers can use it.

For that to be useful it has to be public. A recruiter isn't going to make an account to look at my projects, so there's no sign-in. That means anyone on the internet, and any model they connect, can call every tool on the server.

It's tempting to put more on the same server later. I already have an admin panel for editing projects, regenerating embeddings, and reviewing Job Fit and adversarial test runs, and "manage my portfolio from Claude" would be a fun next step.

The problem is prompt injection. A connected model doesn't keep its user's instructions apart from the text it reads through tools. Everything this server returns is text I wrote, but it also includes job descriptions people paste into the `evaluate_candidate_fit` prompt, and once a client has more than one server connected, text from anywhere else. If any tool on this server could write, then any text the model reads, from any source, is a possible way to make it call that tool. Being careful about which tools a model "should" call doesn't fix that. The only reliable fix is that the dangerous tools aren't there.

## Decision

**The public server at `/mcp` is read-only, and stays that way.** That's enforced in layers, so no single mistake opens it up:

1. Every tool is annotated `ReadOnly = true, Destructive = false`.
2. The tools only call `IPortfolioReadService` and `IPortfolioSearchService`, which have no write methods and return their own DTOs.
3. Those services use `PublicPortfolioDbContext`, which maps four public tables, doesn't track changes, and throws on every `SaveChanges`.
4. That context logs in as the `mcp_readonly` Postgres role, which can only `SELECT` those four tables and whose sessions are read-only by default.

The integration tests check each layer, and one test runs every tool and verifies that no row in any table changed.

**Admin tools, if I build them, go on a separate MCP server.** That means:

- a different endpoint (for example `/mcp/admin`), never more tools added to `/mcp`;
- real auth (OAuth, which MCP supports), so only I can connect;
- its own tool list and its own service interface, so the public read service never grows write methods "just for the admin side";
- not loaded in the same client session as the public server when I use it, so text read from one can't drive tools on the other.

Keeping the public server's interface narrow is part of this. The admin side doesn't share it.

## Consequences

Good:

- The worst a malicious prompt can do through this server is make a model read my public portfolio, which anyone can already do on the website.
- Rate limits, the body cap and the embedding budget are the only things standing between the public and my Azure bill, and they're simple because nothing has side effects.
- Clients can call the tools without asking the user for confirmation each time, since they're declared read-only and actually are.

Less good:

- Two servers is more to build and run than one, if the admin server ever happens.
- New public data needs a mapping in the read-only context **and** a grant in the role script. That's intentional friction, but it is friction.
- There's no auth, so there's also no per-user anything: no saved searches, no "notify me". Anything like that belongs somewhere else.

Later, and compatible with this:

- An **MCP App** (interactive UI returned by a tool, such as a project card or a Skill Map view) fits here, as long as it only renders data the read-only tools already return.
- A **"Connect your AI" page** on the site with the URL and per-client steps is just docs for this server. It doesn't change the decision.
