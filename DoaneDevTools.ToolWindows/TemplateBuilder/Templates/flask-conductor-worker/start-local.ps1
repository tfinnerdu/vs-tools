$env:FLASK_ENV = "development"
$env:SERVICE_NAME = "{{ service_name }}"
$env:PORT = "{{ port }}"
$env:CONDUCTOR_SERVER_URL = "http://localhost:8080/api"
$env:LOG_LEVEL = "DEBUG"

if (!(Test-Path .venv)) {
    python -m venv .venv
    .\.venv\Scripts\pip install -r requirements.txt
}

.\.venv\Scripts\python app.py
