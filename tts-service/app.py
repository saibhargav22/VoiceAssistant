import io
import numpy as np
import soundfile as sf
from flask import Flask, request, jsonify, send_file
from kokoro_onnx import Kokoro

app = Flask(__name__)

print("Loading Kokoro TTS model...")
kokoro = Kokoro("kokoro-v1.0.onnx", "voices-v1.0.bin")
print("Kokoro TTS model ready.")

@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "ok", "model": "kokoro-82m"})

@app.route("/synthesise", methods=["POST"])
def synthesise():
    data = request.get_json()
    if not data or "text" not in data:
        return jsonify({"error": "no text provided"}), 400

    text = data["text"]
    voice = data.get("voice", "af_heart")
    speed = float(data.get("speed", 1.0))

    try:
        samples, sample_rate = kokoro.create(text, voice=voice, speed=speed, lang="en-us")
        buf = io.BytesIO()
        sf.write(buf, samples, sample_rate, format="WAV")
        buf.seek(0)
        return send_file(buf, mimetype="audio/wav", as_attachment=False)
    except Exception as e:
        return jsonify({"error": str(e)}), 500

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5002, debug=False)
