#!/bin/bash

echo "Starting STT service..."
cd ~/VoiceAssistant/stt-service
source venv/bin/activate
python3 app.py &
STT_PID=$!

echo "Starting TTS service..."
cd ~/VoiceAssistant/tts-service
source venv/bin/activate
python3 app.py &
TTS_PID=$!

echo "Starting OCR service..."
cd ~/VoiceAssistant/ocr-service
source venv/bin/activate
python3 app.py &
OCR_PID=$!

echo "Waiting for Python services to load models..."

wait_for_health() {
  local name="$1" url="$2" retries=40
  for i in $(seq 1 $retries); do
    if curl -sf "$url" > /dev/null 2>&1; then
      echo "  $name is ready."
      return 0
    fi
    sleep 2
  done
  echo "  WARNING: $name did not become healthy after $((retries * 2))s — starting API anyway."
}

wait_for_health "STT" "http://localhost:5001/health"
wait_for_health "TTS" "http://localhost:5002/health"
wait_for_health "OCR" "http://localhost:5003/health"

echo "Starting .NET API..."
cd ~/VoiceAssistant
dotnet run --project VoiceAssistant.API &
API_PID=$!

echo ""
echo "All services running."
echo "  STT  → http://localhost:5001"
echo "  TTS  → http://localhost:5002"
echo "  OCR  → http://localhost:5003"
echo "  API  → http://0.0.0.0:5000"
echo "  HTTPS→ https://192.168.1.106:5443"
echo ""
echo "Press Ctrl+C to stop all services."

trap "kill $STT_PID $TTS_PID $OCR_PID $API_PID 2>/dev/null; exit" SIGINT SIGTERM
wait
