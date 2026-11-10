import logging

import structlog

SERVICE_NAME = "agent-service"

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


def configure_logging(telemetry: bool = False) -> None:
    """
    Keep structlog's default console output, and forward to App Insights when telemetry
    is on (see telemetry.py). Without it (local dev by default) it's console only.
    Nothing to flush on shutdown: the OpenTelemetry SDK flushes when the process exits.
    """
    if not telemetry:
        return

    # Insert right before the ConsoleRenderer so the bridge gets the structured dict
    processors = list(structlog.get_config()["processors"])
    processors.insert(len(processors) - 1, OtelBridge())
    structlog.configure(processors=processors)
