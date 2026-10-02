import asyncio
import json
import structlog
import aio_pika
from messaging.connection import get_channel, QUEUES

logger = structlog.get_logger()


async def start_consumers() -> None:
    """Start all background queue consumers. Called on app startup."""
    channel = await get_channel()

    for queue_key, queue_name in QUEUES.items():
        queue = await channel.get_queue(queue_name)
        await queue.consume(_make_handler(queue_key))
        logger.info("queue_consumer_started", queue=queue_name)


def _make_handler(queue_key: str):
    async def handler(message: aio_pika.IncomingMessage) -> None:
        async with message.process(requeue_on_fail=True):
            try:
                body = json.loads(message.body.decode())
                logger.info("queue_message_received", queue=queue_key, body=body)
                await _dispatch(queue_key, body)
            except Exception as e:
                logger.error("queue_message_failed", queue=queue_key, error=str(e))
                raise
    return handler


async def _dispatch(queue_key: str, body: dict) -> None:
    if queue_key == "portfolio.curator.analyze":
        from agents.portfolio_curator.agent import run_curator
        await run_curator(body)
    elif queue_key == "portfolio.adversarial.test":
        from agents.adversarial.agent import run_adversarial_test
        await run_adversarial_test(body)
    else:
        logger.warning("queue_unknown_key", queue_key=queue_key)
