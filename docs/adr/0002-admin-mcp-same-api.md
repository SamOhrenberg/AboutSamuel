# 2. The admin MCP server is a second route on the same API

- **Status:** Accepted
- **Date:** 2026-11-17

## Context

ADR 0001 kept `/mcp` read-only and said admin tools, if I built them, would go on a separate MCP server with real auth, its own tools and its own service interface. I want to manage the site from Claude, including on my phone, so now I'm building it.

"Separate server" could mean a separate deployment. The API runs on Railway, and a second service for one user's admin traffic is more to run and pay for than it's worth. What 0001 actually cares about is that public callers can never reach a write tool, and that the admin side has its own auth and its own surface. None of that needs a second process.

Claude.ai and the Claude apps only connect to authenticated remote servers through MCP's OAuth flow, so a static API key isn't an option if I want it on my phone. My admin login is a magic link, not a password, and I'd like to keep it that way.

## Decision

**The admin server is `/mcp/admin` on the existing API.** It shares the process and the database with `/mcp`, and the separation is enforced in layers instead:

1. The admin tools are never registered with the MCP server. The server's default tool set is the public one. A per-request hook (`ConfigureSessionOptions`) swaps in the admin tools, instructions and identity only for requests routed to `/mcp/admin` by a valid admin MCP token.
2. `/mcp/admin` requires its own auth policy and its own bearer scheme. Admin MCP tokens use a different signing key (derived from the admin JWT secret) and a different audience from admin panel tokens, so neither works in the other's place.
3. A call-tool filter refuses any `admin_` tool unless the caller is an admin on `/mcp/admin`, and a list-tools filter drops `admin_` tools from every other listing. Every admin tool name starts with `admin_`, checked at startup.
4. The admin tools call shared admin services (`Services/Admin`), which the admin panel's controllers now use too. The public read service is unchanged and still has no write methods.

Tests check that `/mcp` lists exactly the public tools before, after and interleaved with admin requests, and that admin tools can't be called there.

**OAuth, with my email as the login.** The API is its own authorization server, for one user:

- Clients register themselves (RFC 7591), as MCP expects, but only with redirect URIs from an allowlist: Claude's callbacks, plus loopback for desktop clients.
- `/oauth/authorize` emails me an approval link and shows a short code in the window that started the sign-in. The approval page wants that code typed in. A sign-in someone else started can't be approved by mistake, because I never saw its code. Opening the link doesn't approve anything; that takes a POST, so mail scanners can't.
- The starting window polls and collects the authorization code itself, so it works when the email opens in a different app or browser.
- PKCE (S256) is required. Access tokens last an hour. Refresh tokens rotate, and reusing one (or replaying a code) revokes the whole session. A session lasts at most 90 days, then I approve again. Revocation is checked on every request, and the admin panel can revoke every session at once.

**Text other people wrote stays data.** The admin tools can read chat logs, Job Fit runs and recruiter emails, which strangers wrote, on the same server that can write. The server instructions and those tools' descriptions say to treat that text as data, never as instructions. Every write tool is annotated as such (deletes, hides and updates as destructive), and I set the write tools to "needs approval" in Claude's connector settings, so nothing changes without me reading it first. Adversarial test results go further: the tools return verdicts and the judge's explanation, never the attack prompts or SamuelLM's answers, which are prompt injections by design.

## Consequences

Good:

- One service to run. The admin panel and the admin tools share their validation, so they can't drift apart.
- Claude.ai, the Claude apps and Claude Code can all connect with no secret on my side to store.
- Every admin tool call is logged with the client and session, and writes are logged with their arguments.

Less good:

- A bug in the routing hook could put admin tools on `/mcp`. The filters and the auth policy are there so that bug alone wouldn't be enough, and the tests cover it, but it's a risk a separate deployment wouldn't have.
- The sign-in is a little slower than a password: switch to email, type an eight-character code.
- The write tools do whatever I approve. Approval prompts are the real guard against injected text, so they only work if I actually read them.

Not included:

- Data import and export. A tool that overwrites tables from JSON isn't worth the risk, and export is a file download the panel already does.
- Uploading a resume PDF. Models don't handle binary files well; the panel does it.
