import os
import logging
from conductor.client.worker.worker_task import worker_task

logger = logging.getLogger(__name__)


@worker_task(task_definition_name="{{ worker_task_name }}")
def {{ worker_task_name }}_task(input_data: dict) -> dict:
    """
    Conductor worker task: {{ worker_task_name }}

    Expected input keys:
        - (define your input schema here)

    Returns:
        dict with keys: status, output, message
    """
    logger.info("Executing {{ worker_task_name }} with input: %s", input_data)

    try:
        # TODO: implement task logic
        result = {}

        return {
            "status": "COMPLETED",
            "output": result,
            "message": "{{ worker_task_name }} completed successfully"
        }
    except Exception as exc:
        logger.exception("{{ worker_task_name }} failed")
        return {
            "status": "FAILED",
            "output": {},
            "message": str(exc)
        }
