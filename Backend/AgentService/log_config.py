import logging
import queue
import sys
import threading
import time
import traceback
from datetime import datetime, timezone

import structlog
from axiom_py import Client

from config import get_settings

SERVICE_NAME = "agent-service"
FLUSH_INTERVAL_SECONDS = 2
MAX_BATCH_SIZE = 500


class AxiomShipper:
    """
    structlog processor that ships events to Axiom without blocking the event loop.

    The axiom-py processors either make a blocking HTTP call inside the log call
    (stalls token streaming) or need `await logger.ainfo(...)` everywhere. This one
    just drops the event on a queue, and a background thread sends batches.
    """

    def __init__(self, token: str, dataset: str):
        self._client = Client(token)
        self._dataset = dataset
        self._queue: queue.Queue[dict] = queue.Queue()
        self._thread = threading.Thread(target=self._run, name="axiom-shipper", daemon=True)
        self._thread.start()

    def __call__(self, logger, method_name: str, event_dict: dict) -> dict:
        self._queue.put(_to_axiom_event(event_dict))
        return event_dict

    def flush(self) -> None:
        batch = []
        while len(batch) < MAX_BATCH_SIZE:
            try:
                batch.append(self._queue.get_nowait())
            except queue.Empty:
                break
        if not batch:
            return
        try:
            self._client.ingest_events(self._dataset, batch)
        except Exception as e:
            # Can't log this through structlog without looping back in here
            print(f"axiom_ingest_failed: {e}", file=sys.stderr)

    def _run(self) -> None:
        while True:
            time.sleep(FLUSH_INTERVAL_SECONDS)
            self.flush()


def _to_axiom_event(event_dict: dict) -> dict:
    event = {"_time": datetime.now(timezone.utc).isoformat(), "service": SERVICE_NAME}
    for key, value in event_dict.items():
        if key == "exc_info" and value:
            exc = sys.exc_info() if value is True else value
            event["exception"] = "".join(traceback.format_exception(*exc))
        elif isinstance(value, (str, int, float, bool, type(None))):
            event[key] = value
        else:
            event[key] = str(value)
    return event


# Attributes a LogRecord already has. Passing one of these in `extra` raises KeyError.
_RESERVED = set(vars(logging.makeLogRecord({}))) | {"message", "asctime"}
_LEVELS = logging.getLevelNamesMapping()


class OtelBridge:
    """
    structlog processor that re-emits each event through stdlib logging, where the
    Azure Monitor distro's handler (telemetry.py) exports it to App Insights. The record
    is created inside whatever span is current, so the log line lands on that trace.
    Event fields become customDimensions.
    """

    def __init__(self):
        self._logger = logging.getLogger(SERVICE_NAME)
        # A new logger inherits root's WARNING, which would drop info events.
        self._logger.setLevel(logging.INFO)

    def __call__(self, logger, method_name: str, event_dict: dict) -> dict:
        level = _LEVELS.get(str(event_dict.get("level", method_name)).upper(), logging.INFO)
        extra = {}
        for key, value in event_dict.items():
            if key in ("event", "level", "exc_info", "timestamp"):
                continue
            name = f"{key}_" if key in _RESERVED else key
            extra[name] = value if isinstance(value, (str, int, float, bool)) else str(value)
        self._logger.log(
            level,
            str(event_dict.get("event", "")),
            exc_info=event_dict.get("exc_info") or None,
            extra=extra,
        )
        return event_dict


_shipper: AxiomShipper | None = None


def configure_logging(telemetry: bool = False) -> None:
    """
    Keep structlog's default console output, and also:
      - ship to Axiom when AXIOM_TOKEN and AXIOM_DATASET are set
      - forward to App Insights when telemetry is on (see telemetry.py)
    Without either (local dev) it's console only.
    """
    global _shipper
    settings = get_settings()
    extra_processors = []
    if settings.axiom_token and settings.axiom_dataset:
        _shipper = AxiomShipper(settings.axiom_token, settings.axiom_dataset)
        extra_processors.append(_shipper)
    if telemetry:
        extra_processors.append(OtelBridge())
    if not extra_processors:
        return

    # Insert right before the ConsoleRenderer so they get the structured dict
    processors = list(structlog.get_config()["processors"])
    processors[-1:-1] = extra_processors
    structlog.configure(processors=processors)


def flush_logs() -> None:
    if _shipper:
        _shipper.flush()
