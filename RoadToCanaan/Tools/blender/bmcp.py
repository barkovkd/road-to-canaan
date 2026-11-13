#!/usr/bin/env python3
"""Drives the live Blender through the "MCP for Blender" server (the `uvx blender-mcp` command from the
Claude config) over MCP stdio. Blender must be running with the add-on's server started (port 9876).

  bmcp.py status
  bmcp.py run <script.py> [key=value ...]    run a script file inside Blender; ARGS holds the key=value pairs
  bmcp.py code '<python>'                    run a line of Python inside Blender
  bmcp.py look <out_prefix> ['<json args>']  save the images the look tool returns as <out_prefix>_N.png
  bmcp.py info ['<json args>']               get_scene_info

Each Blender call must finish within 180 s (the server's socket timeout): split long jobs.
"""
import base64
import json
import os
import subprocess
import sys
import threading
import time

SERVER = [os.path.expanduser("~/.local/bin/uvx"), "blender-mcp"]
ENV = {"DISABLE_TELEMETRY": "true", "BLENDER_HOST": "127.0.0.1"}
HERE = os.path.dirname(os.path.abspath(__file__))


class Mcp:
    def __init__(self):
        env = dict(os.environ)
        env.update(ENV)
        self.p = subprocess.Popen(SERVER, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env)
        self.err = []
        threading.Thread(target=self._drain, daemon=True).start()
        self.id = 0
        self.req("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                                "clientInfo": {"name": "canaan-tools", "version": "1"}})
        self.note("notifications/initialized")

    def _drain(self):
        for line in self.p.stderr:
            self.err.append(line.decode("utf-8", "replace").rstrip())

    def _send(self, msg):
        self.p.stdin.write((json.dumps(msg) + "\n").encode("utf-8"))
        self.p.stdin.flush()

    def note(self, method, params=None):
        self._send({"jsonrpc": "2.0", "method": method, "params": params or {}})

    def req(self, method, params=None):
        self.id += 1
        self._send({"jsonrpc": "2.0", "id": self.id, "method": method, "params": params or {}})
        while True:
            line = self.p.stdout.readline()
            if not line:
                raise RuntimeError("MCP server closed the pipe:\n" + "\n".join(self.err[-12:]))
            try:
                m = json.loads(line)
            except ValueError:
                continue
            if m.get("id") == self.id:
                if "error" in m:
                    raise RuntimeError("MCP error: " + json.dumps(m["error"]))
                return m["result"]

    def tool(self, name, args=None):
        """Returns (text, [image bytes], is_error)."""
        res = self.req("tools/call", {"name": name, "arguments": args or {}})
        texts, images = [], []
        for c in res.get("content", []):
            if c.get("type") == "text":
                texts.append(c["text"])
            elif c.get("type") == "image":
                images.append(base64.b64decode(c["data"]))
        text = "\n".join(texts)
        bad = bool(res.get("isError")) or text.startswith("Error")
        return text, images, bad

    def close(self):
        for f in (self.p.stdin.close, self.p.terminate):
            try:
                f()
            except Exception:
                pass


def run_script(m, path, args):
    path = os.path.abspath(path)
    stub = (
        "import sys\n"
        "ARGS = %r\n"
        "__file__ = %r\n"
        "if %r not in sys.path: sys.path.insert(0, %r)\n"
        "exec(compile(open(__file__, encoding='utf-8').read(), __file__, 'exec'))\n"
    ) % (args, path, HERE, HERE)
    return m.tool("execute_blender_code", {"code": stub})


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    cmd = sys.argv[1]
    t0 = time.time()
    m = Mcp()
    try:
        if cmd == "status":
            text, _, bad = m.tool("get_addon_status")
        elif cmd == "run":
            args = {}
            for kv in sys.argv[3:]:
                k, _, v = kv.partition("=")
                args[k] = v
            text, _, bad = run_script(m, sys.argv[2], args)
        elif cmd == "code":
            text, _, bad = m.tool("execute_blender_code", {"code": sys.argv[2]})
        elif cmd == "look":
            prefix = sys.argv[2]
            args = json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
            text, images, bad = m.tool("look", args)
            for i, data in enumerate(images):
                out = "%s_%d.png" % (prefix, i)
                with open(out, "wb") as f:
                    f.write(data)
                text += "\nsaved " + out
        elif cmd == "info":
            text, _, bad = m.tool("get_scene_info", json.loads(sys.argv[2]) if len(sys.argv) > 2 else {})
        else:
            print(__doc__)
            return 2
    finally:
        m.close()
    if text.startswith("Code executed successfully: "):
        text = text[len("Code executed successfully: "):]
    print(text)
    print("[%s %.1fs%s]" % (cmd, time.time() - t0, " FAILED" if bad else ""))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
