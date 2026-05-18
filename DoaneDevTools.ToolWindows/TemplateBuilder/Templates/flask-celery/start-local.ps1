$env:FLASK_ENV = "development"
$env:SERVICE_NAME = "{{ service_name }}"
$env:PORT = "{{ port }}"
$env:REDIS_URL = "redis://localhost:6379/0"
$env:LOG_LEVEL = "DEBUG"

# Start Celery worker in a separate window
Start-Process powershell -ArgumentList "-NoExit", "-Command", ".\.venv\Scripts\celery -A celery_app worker --loglevel=info -Q {{ celery_queue }}"

if (!(Test-Path .venv)) {
    python -m venv .venv
    .\.venv\Scripts\pip install -r requirements.txt
}
.\.venv\Scripts\python app.py
