import os
import subprocess
import tempfile
from flask import Flask, request, jsonify
from faster_whisper import WhisperModel

app = Flask(__name__)

print("Loading Whisper model...")
model = WhisperModel("small", device="cpu", compute_type="int8")
print("Whisper model ready.")

@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "ok", "model": "whisper-small"})

@app.route("/transcribe", methods=["POST"])
def transcribe():
    if "audio" not in request.files:
        return jsonify({"error": "no audio file provided"}), 400

    audio_file = request.files["audio"]
    suffix = ".webm" if "webm" in audio_file.content_type else ".wav"

    with tempfile.NamedTemporaryFile(suffix=suffix, delete=False) as tmp:
        audio_file.save(tmp.name)
        tmp_path = tmp.name

    wav_path = tmp_path.replace(suffix, ".wav")

    try:
        if suffix == ".webm":
            subprocess.run(
                ["ffmpeg", "-y", "-i", tmp_path, wav_path],
                capture_output=True, check=True
            )
        else:
            wav_path = tmp_path

        segments, info = model.transcribe(wav_path, beam_size=2, language="en", vad_filter=True)
        text = " ".join(segment.text for segment in segments).strip()
        return jsonify({
            "text": text,
            "language": info.language,
            "duration": round(info.duration, 2)
        })
    except Exception as e:
        return jsonify({"error": str(e)}), 500
    finally:
        os.unlink(tmp_path)
        if wav_path != tmp_path and os.path.exists(wav_path):
            os.unlink(wav_path)

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5001, debug=False)
