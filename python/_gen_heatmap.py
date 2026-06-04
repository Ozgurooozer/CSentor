"""Generate heatmap from existing test CSV files."""
import os, csv, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from test_runner import generate_heatmap, TestRecording

test_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "test_output")
os.makedirs(test_dir, exist_ok=True)

for fname in sorted(os.listdir(test_dir)):
    if not fname.endswith(".csv") or fname.startswith("_"):
        continue
    fpath = os.path.join(test_dir, fname)
    rec = TestRecording()
    with open(fpath, encoding="utf-8") as f:
        reader = csv.reader(f)
        started = False
        in_annotations = False
        in_ap = False
        for row in reader:
            if not row:
                continue
            if row[0] == "time_abs" and not started:
                started = True
                continue
            if row[0] == "time_abs" and started:
                in_annotations = True
                continue
            if row[0].startswith("anno"):
                continue
            if row[0].startswith("ap_"):
                in_ap = True
                continue
            if in_ap:
                continue
            if in_annotations:
                continue
            if not started:
                if row[0] == "test_name":
                    rec.name = row[1]
                elif row[0] == "duration_s":
                    rec.duration = float(row[1])
                continue
            try:
                t = float(row[1])
                rec.timestamps.append(t)
                rec.rssi_dbm.append(int(row[2]))
                rec.rssi_pct.append(int(row[3]))
                rec.var.append(float(row[4]))
                rec.delta.append(float(row[5]))
                rec.ptp.append(float(row[6]))
                rec.rx_rate.append(float(row[7]) if row[7] else 0)
                rec.tx_rate.append(float(row[8]) if row[8] else 0)
            except (ValueError, IndexError):
                continue

    if len(rec.timestamps) < 2:
        continue

    # Set start_time so timestamps are relative
    t0 = rec.timestamps[0]
    rec.timestamps = [ts - t0 for ts in rec.timestamps]
    rec.start_time = 0

    # Generate heatmap
    out_name = fname.replace(".csv", ".png")
    out_path = os.path.join(test_dir, out_name)
    result = generate_heatmap(rec, out_path)
    if result:
        print(f"OK: {fname} -> {out_name}  ({len(rec.timestamps)} samples)")
    else:
        print(f"FAIL: {fname}")
