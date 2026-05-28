import sys
import os
import json
import asyncio
import edge_tts
from http.server import HTTPServer, BaseHTTPRequestHandler

AUDIO_DIR = os.path.join(
    os.path.expanduser("~"),
    "Documents", "GTA5MOD2026", "audio")
os.makedirs(AUDIO_DIR, exist_ok=True)

EMOTIONS = {
    "angry":    {"rate": "+30%", "pitch": "-10Hz", "volume": "+20%"},
    "scared":   {"rate": "+50%", "pitch": "+15Hz", "volume": "+10%"},
    "fear":     {"rate": "+50%", "pitch": "+15Hz", "volume": "+10%"},  # alias for scared
    "happy":    {"rate": "+15%", "pitch": "+5Hz",  "volume": "+0%"},
    "sad":      {"rate": "-15%", "pitch": "-8Hz",  "volume": "-10%"},
    "cold":     {"rate": "-20%", "pitch": "-5Hz",  "volume": "-5%"},
    "neutral":  {"rate": "+0%",  "pitch": "+0Hz",  "volume": "+0%"},
    "surprise": {"rate": "+40%", "pitch": "+20Hz", "volume": "+15%"},
    "disgust":  {"rate": "-10%", "pitch": "-8Hz",  "volume": "+5%"},
}


class TTSHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass

    def do_POST(self):
        try:
            length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(length).decode("utf-8")
            data = json.loads(body)

            text = data.get("text", "")
            voice = data.get("voice", "zh-CN-YunxiNeural")
            emotion = data.get("emotion", "neutral")

            if not text:
                self.send_response(400)
                self.end_headers()
                self.wfile.write(b'{"error":"no text"}')
                return

            emo = EMOTIONS.get(emotion, EMOTIONS["neutral"])

            filename = f"tts_{hash(text + voice) & 0xFFFFFFFF:08x}.mp3"
            filepath = os.path.join(AUDIO_DIR, filename)

            async def generate():
                communicate = edge_tts.Communicate(
                    text, voice,
                    rate=emo["rate"],
                    pitch=emo["pitch"],
                    volume=emo["volume"])
                await communicate.save(filepath)

            asyncio.run(generate())

            if os.path.exists(filepath):
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                response = json.dumps({"file": filepath})
                self.wfile.write(response.encode("utf-8"))
            else:
                self.send_response(500)
                self.end_headers()
                self.wfile.write(b'{"error":"generation failed"}')
        except Exception as e:
            self.send_response(500)
            self.end_headers()
            self.wfile.write(json.dumps({"error": str(e)}).encode("utf-8"))


def main():
    port = 5111
    server = HTTPServer(("127.0.0.1", port), TTSHandler)
    print(f"SENTIENCE Voice Server running on port {port}")
    print("Do not close this window!")
    print("请勿关闭此窗口！")
    server.serve_forever()


if __name__ == "__main__":
    main()
