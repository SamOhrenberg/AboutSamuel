using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace PortfolioWebsite.Api.AdminMcp.OAuth;

/// <summary>
/// The few HTML pages of the sign-in flow, rendered here rather than in the Vue site so the
/// whole flow lives on the API origin. Every dynamic value is HTML-encoded (the client name is
/// whatever the client registered), and each page carries a strict CSP with a per-response
/// script nonce.
/// </summary>
public static class AdminOAuthPages
{
    private const string Styles = """
        :root { color-scheme: light dark; --fg:#1b1f23; --muted:#5f6b76; --bg:#f6f8f9; --card:#fff; --line:#d8dee3; --accent:#007c7c; --bad:#b42318; }
        @media (prefers-color-scheme: dark) { :root { --fg:#e6edf3; --muted:#9aa7b2; --bg:#0f1417; --card:#171d21; --line:#2b343b; --accent:#2bb3b3; --bad:#f97066; } }
        * { box-sizing: border-box; }
        body { margin:0; padding:24px 16px; background:var(--bg); color:var(--fg); font:16px/1.5 system-ui, -apple-system, "Segoe UI", sans-serif; }
        main { max-width:460px; margin:0 auto; background:var(--card); border:1px solid var(--line); border-radius:12px; padding:24px; }
        h1 { font-size:20px; margin:0 0 12px; }
        p { margin:0 0 12px; }
        .muted { color:var(--muted); font-size:14px; }
        .code { font:600 32px/1.2 ui-monospace, "Cascadia Mono", Menlo, monospace; letter-spacing:4px; text-align:center; padding:16px; border:1px dashed var(--line); border-radius:8px; margin:16px 0; }
        .bad { color:var(--bad); }
        dl { display:grid; grid-template-columns:auto 1fr; gap:4px 12px; margin:0 0 16px; font-size:14px; }
        dt { color:var(--muted); } dd { margin:0; overflow-wrap:anywhere; }
        input[type=text] { width:100%; font:600 20px ui-monospace, Menlo, monospace; letter-spacing:2px; padding:10px 12px; border:1px solid var(--line); border-radius:8px; background:transparent; color:inherit; text-transform:uppercase; }
        .row { display:flex; gap:8px; margin-top:16px; flex-wrap:wrap; }
        button { flex:1; min-width:120px; font:inherit; padding:10px 16px; border-radius:8px; border:1px solid var(--line); background:transparent; color:inherit; cursor:pointer; }
        button.primary { background:var(--accent); border-color:var(--accent); color:#fff; }
        """;

    /// <summary>Shown in the window that started the sign-in. Polls until Samuel decides.</summary>
    public static IResult Waiting(AuthorizeOutcome.Started started)
    {
        var nonce = Nonce();
        var config = JsonSerializer.Serialize(new { id = started.Id, key = started.BrowserKey });
        var body = $$"""
            <h1>Approve this sign-in</h1>
            <p><strong>{{E(started.ClientName)}}</strong> wants admin access to AboutSamuel.</p>
            <p>An approval link was emailed to Samuel. Open it and enter this code:</p>
            <div class="code" aria-label="Approval code">{{E(started.UserCode)}}</div>
            <p id="status" class="muted" role="status">Waiting for approval. This page continues by itself.</p>
            <p class="muted">Expires at {{started.ExpiresAt:HH:mm}} UTC.</p>
            <script nonce="{{nonce}}">
            (() => {
              const cfg = {{config}};
              const status = document.getElementById("status");
              const say = (text, bad) => { status.textContent = text; status.className = bad ? "bad" : "muted"; };
              async function poll() {
                try {
                  const res = await fetch("/oauth/authorize/poll", {
                    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(cfg)
                  });
                  if (res.status === 429) return setTimeout(poll, 5000);
                  const data = await res.json();
                  if (data.redirect) { say(data.status === "approved" ? "Approved. Returning to the app..." : "Denied. Returning to the app...", data.status !== "approved"); location.replace(data.redirect); return; }
                  if (data.status === "pending") return setTimeout(poll, 2000);
                  if (data.status === "expired") return say("This sign-in expired. Start again from the app.", true);
                  say("This sign-in can't continue. Start again from the app.", true);
                } catch { setTimeout(poll, 4000); }
              }
              setTimeout(poll, 2000);
            })();
            </script>
            """;
        return Page("Approve sign-in", body, nonce);
    }

    /// <summary>The page the emailed link opens. Approving needs the code from the other window.</summary>
    public static IResult Approval(string token, ApprovalView view, string? error = null)
    {
        var body = $"""
            <h1>Admin MCP sign-in</h1>
            <p><strong>{E(view.ClientName)}</strong> is asking for full admin access: it can read and change
            everything in the admin panel.</p>
            <dl>
              <dt>Returns to</dt><dd>{E(view.RedirectHost)}</dd>
              <dt>Requested from</dt><dd>{E(view.RequestIp ?? "unknown")}</dd>
              <dt>Requested at</dt><dd>{view.CreatedAt:yyyy-MM-dd HH:mm} UTC</dd>
            </dl>
            <p>Only approve if you just clicked Connect yourself. Enter the code shown in that window.</p>
            {(error is null ? "" : $"<p class=\"bad\" role=\"alert\">{E(error)}</p>")}
            <form method="post" action="/oauth/approve" autocomplete="off">
              <input type="hidden" name="token" value="{E(token)}">
              <label for="code" class="muted">Code</label>
              <input id="code" name="code" type="text" inputmode="text" maxlength="12" placeholder="XXXX-XXXX" autofocus>
              <div class="row">
                <button class="primary" type="submit" name="decision" value="approve">Approve</button>
                <button type="submit" name="decision" value="deny">Deny</button>
              </div>
            </form>
            <p class="muted" style="margin-top:16px">{view.AttemptsLeft} attempts left. Expires at {view.ExpiresAt:HH:mm} UTC.</p>
            """;
        return Page("Approve sign-in", body);
    }

    public static IResult Message(string title, string message, int statusCode = 200) =>
        Page(title, $"<h1>{E(title)}</h1><p>{E(message)}</p>", statusCode: statusCode);

    private static IResult Page(string title, string body, string? nonce = null, int statusCode = 200)
    {
        var html = $"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex">
            <title>{E(title)} - AboutSamuel</title>
            <style>{Styles}</style>
            </head>
            <body><main>
            {body}
            </main></body>
            </html>
            """;
        var script = nonce is null ? "'none'" : $"'nonce-{nonce}'";
        return new HtmlResult(html, statusCode,
            $"default-src 'none'; script-src {script}; style-src 'unsafe-inline'; connect-src 'self'; " +
            "form-action 'self'; frame-ancestors 'none'; base-uri 'none'");
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string Nonce() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

    private sealed class HtmlResult(string html, int statusCode, string csp) : IResult
    {
        public Task ExecuteAsync(HttpContext context)
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = csp;
            headers.CacheControl = "no-store";
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/html; charset=utf-8";
            return context.Response.WriteAsync(html);
        }
    }
}
