"""
One-time Gmail authorization for recruiter triage. Run it locally, not on Railway:

    python scripts/gmail_auth.py path/to/client_secret.json

It opens a browser so you can sign in and approve access. Google will warn that the app
isn't verified (it's a personal-use app, so that's expected): click Advanced, then
continue. When it finishes it prints three values to set as environment variables, in
your local .env and on the agent service in Railway:

    GMAIL_CLIENT_ID, GMAIL_CLIENT_SECRET, GMAIL_REFRESH_TOKEN

The refresh token is what lets the service read and draft mail without you signing in
again. Treat it like a password. It stops working if you revoke the app's access, change
your Google password, or it goes unused for 6 months. If that happens, run this again.
"""
import json
import sys

from google_auth_oauthlib.flow import InstalledAppFlow

# Read, label, draft, and send. Not permanent delete.
SCOPES = ["https://www.googleapis.com/auth/gmail.modify"]


def main() -> None:
    if len(sys.argv) != 2:
        sys.exit("Usage: python scripts/gmail_auth.py path/to/client_secret.json")

    flow = InstalledAppFlow.from_client_secrets_file(sys.argv[1], SCOPES)
    # prompt=consent makes Google issue a refresh token even if you've authorized before
    creds = flow.run_local_server(port=0, access_type="offline", prompt="consent")
    if not creds.refresh_token:
        sys.exit("Google didn't return a refresh token. Revoke the app at "
                 "https://myaccount.google.com/permissions and run this again.")

    client = json.load(open(sys.argv[1], encoding="utf-8"))
    client = client.get("installed") or client.get("web") or {}
    print("\nAuthorized. Set these as environment variables (keep them secret):\n")
    print(f"GMAIL_CLIENT_ID={client.get('client_id')}")
    print(f"GMAIL_CLIENT_SECRET={client.get('client_secret')}")
    print(f"GMAIL_REFRESH_TOKEN={creds.refresh_token}")


if __name__ == "__main__":
    main()
