from http.server import HTTPServer, BaseHTTPRequestHandler
from pymodules.metadata_conversion import MetadataConverter
import sys, json

class Handler(BaseHTTPRequestHandler):
    def good_response(self, data):
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(json.dumps(data).encode("utf-8"))

    def do_POST(self):
        length = int(self.headers.get("content-length"))
        message = json.loads(self.rfile.read(length))
        if self.path == "/API/Alive":
            self.good_response({"result": "success"})
        elif self.path == "/API/ConvertMetadata":
            metadata_converter = MetadataConverter()
            result, metadata_string, error_message = metadata_converter.generate_new_metadata(message["userInput"], message["subfolders"], message["settings"])
            if result:
                self.good_response({"result": "success", "metadata": metadata_string})
            else:
                self.good_response({"result": "fail", "error": error_message})
        else:
            self.send_response(404)
            self.end_headers()
            self.wfile.write(json.dumps({"error": "bad router"}).encode("utf-8"))

    def do_GET(self):
        self.send_response(404)
        self.end_headers()
        self.wfile.write(b"Invalid request - this is a POST only internal server")

def run(port):
    server_address = ("", port)
    httpd = HTTPServer(server_address, Handler)
    print(f"Running on port {port}")

    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        exit(0)

run(int(sys.argv[1]))