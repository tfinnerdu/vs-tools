import logging
from celery_app import celery

logger = logging.getLogger(__name__)


@celery.task(name="{{ service_name }}.sample_task", bind=True, max_retries=3)
def sample_task(self, payload: dict) -> dict:
    """Example async task — replace with real logic."""
    logger.info("Running sample_task with payload: %s", payload)
    try:
        # TODO: implement task
        return {"status": "done", "input": payload}
    except Exception as exc:
        logger.exception("sample_task failed, retrying")
        raise self.retry(exc=exc, countdown=2 ** self.request.retries)
