from http.server import HTTPServer, BaseHTTPRequestHandler
import sys, json, base64
from pathlib import Path
from io import BytesIO
from PIL import Image, PngImagePlugin

LOG_TEXT = ""

def log(text):
    global LOG_TEXT
    LOG_TEXT += text + "\n"
    print(text)
    
class Handler(BaseHTTPRequestHandler):
    def good_response(self, data):
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps(data).encode('utf-8'))

    def do_POST(self):
        length = int(self.headers.get("content-length"))
        message = json.loads(self.rfile.read(length))
        if self.path == "/API/TODO":
            "GRAAAH"
        elif self.path == "/API/ConvertMetadata":
            global LOG_TEXT
            imgs = [Image.open(BytesIO(base64.b64decode(message['image']))).convert('RGB')]
            for img in imgs:
                ""
        else:
            self.send_response(404)
            self.end_headers()
            self.wfile.write(json.dumps({'error': 'bad router'}).encode('utf-8'))

    def do_GET(self):
        self.send_response(404)
        self.end_headers()
        self.wfile.write(b'Invalid request - this is a POST only internal server')

def run(port):
    server_address = ('', port)
    httpd = HTTPServer(server_address, Handler)
    log(f'Running on port {port}')

    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        exit(0)

run(int(sys.argv[1]))