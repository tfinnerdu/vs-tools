import os
import logging
import json
from flask import Flask, jsonify, request
from celery_app import celery
from tasks import sample_task

logging.basicConfig(
    level=os.environ.get("LOG_LEVEL", "INFO"),
    format=json.dumps({
        "time": "%(asctime)s", "level": "%(levelname)s",
        "service": "{{ service_name }}", "message": "%(message)s"
    })
)
logger = logging.getLogger(__name__)

app = Flask(__name__)


@app.route("/health")
def health():
    return jsonify({"status": "ok", "service": "{{ service_name }}"})


@app.route("/api/v1/tasks", methods=["POST"])
def enqueue_task():
    payload = request.get_json(force=True) or {}
    task = sample_task.apply_async(args=[payload], queue="{{ celery_queue }}")
    return jsonify({"task_id": task.id, "status": "queued"}), 202


@app.route("/api/v1/tasks/<task_id>")
def task_status(task_id: str):
    result = celery.AsyncResult(task_id)
    return jsonify({
        "task_id": task_id,
        "status": result.status,
        "result": result.result if result.ready() else None
    })


if __name__ == "__main__":
    port = int(os.environ.get("PORT", {{ port }}))
    logger.info("Starting {{ service_name }} on port %d", port)
    app.run(host="0.0.0.0", port=port, use_reloader=False)
