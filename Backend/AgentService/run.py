import os
import socket

import uvicorn

# Production entrypoint. Binds one dual-stack socket so the service accepts IPv4
# (Railway's healthcheck) and IPv6 (Railway's private network). `uvicorn --host ::`
# can't do this because asyncio sets IPV6_V6ONLY on sockets it creates itself.
# Local dev still uses `uvicorn main:app --reload --port 8000`.
port = int(os.environ.get("PORT", "8000"))

sock = socket.socket(socket.AF_INET6, socket.SOCK_STREAM)
sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
sock.setsockopt(socket.IPPROTO_IPV6, socket.IPV6_V6ONLY, 0)
sock.bind(("::", port))

uvicorn.Server(uvicorn.Config("main:app")).run(sockets=[sock])
