import os
import logging
import json
from flask import Flask, jsonify, send_from_directory

logging.basicConfig(
    level=os.environ.get("LOG_LEVEL", "INFO"),
    format=json.dumps({
        "time": "%(asctime)s", "level": "%(levelname)s",
        "service": "{{ service_name }}", "message": "%(message)s"
    })
)
logger = logging.getLogger(__name__)

STATIC_FOLDER = os.path.join(os.path.dirname(__file__), "frontend", "dist")
app = Flask(__name__, static_folder=STATIC_FOLDER, static_url_path="")


@app.route("/health")
def health():
    return jsonify({"status": "ok", "service": "{{ service_name }}"})


@app.route("/api/v1/", defaults={"path": ""})
@app.route("/api/v1/<path:path>")
def api(path):
    # TODO: add API routes here
    return jsonify({"error": "not implemented"}), 404


@app.route("/", defaults={"path": ""})
@app.route("/<path:path>")
def serve_spa(path):
    if path and os.path.exists(os.path.join(STATIC_FOLDER, path)):
        return send_from_directory(STATIC_FOLDER, path)
    return send_from_directory(STATIC_FOLDER, "index.html")


if __name__ == "__main__":
    port = int(os.environ.get("PORT", {{ port }}))
    logger.info("Starting {{ service_name }} on port %d", port)
    app.run(host="0.0.0.0", port=port, use_reloader=False)
