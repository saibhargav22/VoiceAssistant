import cv2
import numpy as np
import pytesseract
from PIL import Image
import io
from flask import Flask, request, jsonify

app = Flask(__name__)

def preprocess_image(image_bytes):
    # Decode image bytes to numpy array
    nparr = np.frombuffer(image_bytes, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

    # Step 1 — resize if too large (keep aspect ratio)
    h, w = img.shape[:2]
    if w > 1800:
        scale = 1800 / w
        img = cv2.resize(img, (1800, int(h * scale)))

    # Step 2 — convert to grayscale
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

    # Step 3 — denoise
    denoised = cv2.fastNlMeansDenoising(gray, h=10)

    # Step 4 — adaptive threshold (handles uneven lighting from phone camera)
    thresh = cv2.adaptiveThreshold(
        denoised, 255,
        cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
        cv2.THRESH_BINARY,
        31, 10
    )

    # Step 5 — deskew (straighten tilted bill)
    coords = np.column_stack(np.where(thresh > 0))
    if len(coords) > 0:
        angle = cv2.minAreaRect(coords)[-1]
        if angle < -45:
            angle = -(90 + angle)
        else:
            angle = -angle
        if abs(angle) < 15:  # only correct small tilts
            (hh, ww) = thresh.shape[:2]
            center = (ww // 2, hh // 2)
            M = cv2.getRotationMatrix2D(center, angle, 1.0)
            thresh = cv2.warpAffine(thresh, M, (ww, hh),
                flags=cv2.INTER_CUBIC,
                borderMode=cv2.BORDER_REPLICATE)

    # Step 6 — sharpen
    kernel = np.array([[0, -1, 0], [-1, 5, -1], [0, -1, 0]])
    sharpened = cv2.filter2D(thresh, -1, kernel)

    return sharpened

@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "ok", "model": "tesseract+opencv"})

@app.route("/ocr", methods=["POST"])
def ocr():
    if "image" not in request.files:
        return jsonify({"error": "no image provided"}), 400

    image_file = request.files["image"]
    image_bytes = image_file.read()

    try:
        # Preprocess with OpenCV
        processed = preprocess_image(image_bytes)

        # Convert back to PIL for pytesseract
        pil_image = Image.fromarray(processed)

        # Get text with confidence data
        data = pytesseract.image_to_data(
            pil_image,
            output_type=pytesseract.Output.DICT,
            config="--psm 6"
        )

        # Build lines with average confidence
        lines = {}
        for i, text in enumerate(data["text"]):
            text = text.strip()
            if not text:
                continue
            conf = float(data["conf"][i])
            if conf < 0:
                continue
            line_num = data["line_num"][i]
            if line_num not in lines:
                lines[line_num] = {"words": [], "confidences": []}
            lines[line_num]["words"].append(text)
            lines[line_num]["confidences"].append(conf)

        result_lines = []
        for line_num in sorted(lines.keys()):
            line_text = " ".join(lines[line_num]["words"])
            avg_conf = sum(lines[line_num]["confidences"]) / len(lines[line_num]["confidences"])
            result_lines.append({
                "text": line_text,
                "confidence": round(avg_conf / 100, 2)
            })

        full_text = "\n".join(l["text"] for l in result_lines)
        avg_overall = sum(l["confidence"] for l in result_lines) / len(result_lines) if result_lines else 0

        return jsonify({
            "full_text": full_text,
            "lines": result_lines,
            "overall_confidence": round(avg_overall, 2)
        })

    except Exception as e:
        return jsonify({"error": str(e)}), 500

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5003, debug=False)
