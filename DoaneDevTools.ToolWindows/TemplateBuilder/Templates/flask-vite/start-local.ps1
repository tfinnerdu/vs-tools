$env:FLASK_ENV = "development"
$env:PORT = "{{ port }}"
$env:LOG_LEVEL = "DEBUG"

# Start Vite dev server in a separate window
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd frontend; npm run dev"

if (!(Test-Path .venv)) {
    python -m venv .venv
    .\.venv\Scripts\pip install -r requirements.txt
}
.\.venv\Scripts\python app.py
