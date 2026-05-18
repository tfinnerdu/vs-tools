import os
from celery import Celery

REDIS_URL = os.environ.get("REDIS_URL", "redis://localhost:6379/0")

celery = Celery(
    "{{ service_name }}",
    broker=REDIS_URL,
    backend=REDIS_URL,
    include=["tasks"]
)

celery.conf.update(
    task_default_queue="{{ celery_queue }}",
    task_serializer="json",
    result_serializer="json",
    accept_content=["json"],
    timezone="America/Chicago",
    enable_utc=True,
    worker_prefetch_multiplier=1,
    task_acks_late=True,
)
