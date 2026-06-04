import subprocess, json, sys, time

proc = subprocess.Popen(
    [sys.executable, '-X', 'utf8', 'wifi_motion_server.py'],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
    text=True, encoding='utf-8', bufsize=0
)

def rpc(method, params=None):
    req = json.dumps({'jsonrpc': '2.0', 'method': method, 'params': params or {}, 'id': 1})
    proc.stdin.write(req + '\n')
    proc.stdin.flush()
    line = ''
    while True:
        ch = proc.stdout.read(1)
        if not ch: break
        line += ch
        if ch == '\n':
            try: return json.loads(line)
            except: line = ''
    return None

# Init
rpc('initialize', {'protocolVersion': '2024-11-05', 'capabilities': {}, 'clientInfo': {'name': 'test', 'version': '1'}})
resp = rpc('tools/list')
tools = [t['name'] for t in resp.get('result', {}).get('tools', [])]
print(f'Tools ({len(tools)}): {tools}')
print()

# Start detection
resp = rpc('tools/call', {'name': 'start_detection', 'arguments': {}})
print(f'start: {resp["result"]}')
time.sleep(2)

# Start a handwave test
resp = rpc('tools/call', {'name': 'start_test', 'arguments': {'test_type': 'handwave', 'duration': 12}})
print(f'test: {resp["result"]}')

# Monitor
for i in range(6):
    time.sleep(2)
    resp = rpc('tools/call', {'name': 'get_test_status', 'arguments': {}})
    s = resp.get('result', {})
    print(f'  -> {s.get("test_name","?")}  kalan: {s.get("remaining_s",0):.0f}s  ornek: {s.get("samples",0)}')

# Stop test
resp = rpc('tools/call', {'name': 'stop_test', 'arguments': {}})
print(f'stop: {resp["result"][:80]}')

# Final status
resp = rpc('tools/call', {'name': 'get_status', 'arguments': {}})
s = resp.get('result', {})
print(f'final: motion={s.get("motion")} rssi={s.get("rssi_dbm")}dBm var={s.get("var",0):.3e}')

rpc('tools/call', {'name': 'stop_detection', 'arguments': {}})
proc.terminate()
print('Done')
