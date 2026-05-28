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

echo "Waiting for Python services to load models..."
sleep 8

echo "Starting .NET API..."
cd ~/VoiceAssistant
dotnet run --project VoiceAssistant.API &
API_PID=$!

echo ""
echo "All services running."
echo "  STT   → http://localhost:5001"
echo "  TTS   → http://localhost:5002"
echo "  API   → http://0.0.0.0:5000"
echo "  HTTPS → https://192.168.1.106:5443  (use this on phone)"
echo ""
echo "Press Ctrl+C to stop all services."

trap "kill $STT_PID $TTS_PID $API_PID 2>/dev/null; exit" SIGINT SIGTERM
wait
