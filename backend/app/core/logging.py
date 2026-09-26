"""Logging to stdout, with a level from `LOG_LEVEL` and no private data in it.

Plain stdlib `logging` and one line per event in `key=value` form: no dependency,
readable in `docker compose logs`, greppable. JSON would only pay off with a log
shipper, which is out of scope.

**What never goes into a log line:** request bodies, query strings, headers and
cookies, and the *message* of an unexpected exception. Messages carry values —
a database error repeats the bound parameters, a `KeyError` the key — so an error
is logged as its class plus the stack frames, which are code and no data.
"""

import logging
import re
import sys
import traceback

from fastapi.responses import JSONResponse
from starlette.types import ASGIApp, Message, Receive, Scope, Send

logger = logging.getLogger("duofy")

LEVELS = {"DEBUG", "INFO", "WARNING", "ERROR"}
DEFAULT_LEVEL = "INFO"

#: A path segment that is an identifier: a UUID or a number.
_ID_SEGMENT = re.compile(
    r"(?<=/)(?:[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|\d+)(?=/|$)"
)


def route_of(scope: Scope) -> str:
    """The path of the request with identifiers replaced by `{id}`.

    Not the matched route object: FastAPI keeps included routers nested, so its
    path lacks the `/api/v1` prefix. The masked path is complete, and it also keeps
    the ids of somebody's records out of the log.
    """
    return _ID_SEGMENT.sub("{id}", scope["path"])


def configure_logging(level_name: str) -> None:
    """Send everything to stdout at the given level.

    An unknown level falls back to INFO and says so — a typo in the environment
    must not keep the backend from starting, and must not go unnoticed either.
    """
    level = level_name.strip().upper()
    invalid = level not in LEVELS
    if invalid:
        level = DEFAULT_LEVEL

    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(
        logging.Formatter("%(asctime)s level=%(levelname)s logger=%(name)s %(message)s")
    )
    root = logging.getLogger()
    root.handlers = [handler]
    root.setLevel(level)

    if invalid:
        # Only the length of the bad value: it came from the environment and could
        # be anything, including something that should not be in a log.
        logger.warning(
            "event=invalid_log_level value_length=%d fallback=%s",
            len(level_name),
            DEFAULT_LEVEL,
        )


class UnexpectedErrorMiddleware:
    """Log an exception nobody handled and answer with a code, not with details.

    Route, method and status go into the log with the stack; the client gets
    `{"detail": {"code": "internal_error"}}`, which the frontend has a text for.
    Written as plain ASGI so it sees the matched route after the call.
    """

    def __init__(self, app: ASGIApp) -> None:
        self.app = app

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return

        started = False

        async def tracked_send(message: Message) -> None:
            nonlocal started
            if message["type"] == "http.response.start":
                started = True
            await send(message)

        try:
            await self.app(scope, receive, tracked_send)
        except Exception as error:
            logger.error(
                "event=unhandled_exception method=%s route=%s status=500 "
                "code=internal_error error=%s\n%s",
                scope["method"],
                route_of(scope),
                type(error).__name__,
                "".join(traceback.format_tb(error.__traceback__)).rstrip(),
            )
            if started:
                # The response is already on its way; all that is left is to stop.
                raise
            response = JSONResponse({"detail": {"code": "internal_error"}}, status_code=500)
            await response(scope, receive, send)
