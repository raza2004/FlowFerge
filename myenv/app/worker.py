"""Ingestion worker: consumes "document added" messages from RabbitMQ.

Run it as its own process:  python -m app.worker

Message (JSON): {documentId, tenantId, fileName, contentType, objectKey}
Flow: download the file from MinIO, extract text, chunk, embed, store chunks, then tell the .NET
API the result (status Ready or Failed) over its internal endpoint.
"""

import json
import logging
import time
import uuid

import httpx
import pika
from minio import Minio

from app.config import get_settings
from app.db import init_schema
from app.observability import correlation_id, setup_logging
from app.rag.ingest import ingest_document

log = logging.getLogger("worker")


def _download(object_key: str) -> bytes:
    s = get_settings()
    client = Minio(s.minio_endpoint, access_key=s.minio_access_key, secret_key=s.minio_secret_key, secure=s.minio_secure)
    response = client.get_object(s.minio_bucket, object_key)
    try:
        return response.read()
    finally:
        response.close()
        response.release_conn()


def _report(document_id: str, status: str, pages: int = 0, chunks: int = 0, error: str | None = None) -> None:
    s = get_settings()
    headers = {"X-Internal-Key": s.internal_api_key, "X-Correlation-Id": correlation_id.get()}
    body = {"status": status, "pageCount": pages, "chunkCount": chunks, "error": error}
    try:
        httpx.put(f"{s.dotnet_api_url}/api/internal/documents/{document_id}/status", json=body, headers=headers, timeout=10)
    except httpx.HTTPError as ex:
        log.warning("could not report status for %s: %s", document_id, ex)


def handle_message(body: bytes) -> None:
    message = json.loads(body)
    correlation_id.set(message.get("correlationId") or str(uuid.uuid4()))
    document_id = message["documentId"]
    log.info("ingesting %s (%s)", message["fileName"], document_id)
    _report(document_id, "Processing")
    try:
        pages, chunks = ingest_document(
            tenant_id=uuid.UUID(message["tenantId"]),
            document_id=uuid.UUID(document_id),
            document_name=message["fileName"],
            data=_download(message["objectKey"]),
            content_type=message.get("contentType", ""),
        )
        _report(document_id, "Ready", pages, chunks)
    except Exception as ex:
        # A bad file will fail again on redelivery, so report it and acknowledge the message.
        log.exception("ingestion failed")
        _report(document_id, "Failed", error=str(ex)[:500])


def main() -> None:
    setup_logging()
    s = get_settings()
    init_schema()
    while True:
        try:
            connection = pika.BlockingConnection(pika.URLParameters(s.rabbitmq_url))
            channel = connection.channel()
            channel.queue_declare(queue=s.ingest_queue, durable=True)
            channel.basic_qos(prefetch_count=1)

            def on_message(ch, method, _props, body):
                handle_message(body)
                ch.basic_ack(delivery_tag=method.delivery_tag)

            channel.basic_consume(queue=s.ingest_queue, on_message_callback=on_message)
            log.info("waiting for documents on %s", s.ingest_queue)
            channel.start_consuming()
        except pika.exceptions.AMQPConnectionError:
            log.warning("RabbitMQ not reachable, retrying in 5s")
            time.sleep(5)


if __name__ == "__main__":
    main()
