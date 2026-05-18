import os
import logging
import json
from flask import Flask, jsonify
from worker import {{ worker_task_name }}_task

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


if __name__ == "__main__":
    port = int(os.environ.get("PORT", {{ port }}))
    logger.info("Starting {{ service_name }} on port %d", port)
    # Register Conductor worker — blocks until stopped
    from conductor.client.automator.task_handler import TaskHandler
    from conductor.client.configuration.configuration import Configuration
    conductor_server = os.environ.get("CONDUCTOR_SERVER_URL", "http://localhost:8080/api")
    config = Configuration(server_api_url=conductor_server)
    with TaskHandler(workers=[{{ worker_task_name }}_task], configuration=config) as task_handler:
        task_handler.start_processes()
    app.run(host="0.0.0.0", port=port, use_reloader=False)
