import aio_pika
from config import get_settings

_connection: aio_pika.RobustConnection | None = None
_channel: aio_pika.RobustChannel | None = None

QUEUES = {
    "portfolio.curator.analyze": "portfolio.curator.analyze",
    "portfolio.adversarial.test": "portfolio.adversarial.test",
}


async def get_channel() -> aio_pika.RobustChannel:
    global _connection, _channel
    if _connection is None or _connection.is_closed:
        settings = get_settings()
        _connection = await aio_pika.connect_robust(settings.rabbitmq_url)
    if _channel is None or _channel.is_closed:
        _channel = await _connection.channel()
        await _channel.set_qos(prefetch_count=1)
        for queue_name in QUEUES.values():
            await _channel.declare_queue(queue_name, durable=True)
    return _channel


async def close_connection() -> None:
    global _connection, _channel
    if _channel:
        await _channel.close()
    if _connection:
        await _connection.close()
