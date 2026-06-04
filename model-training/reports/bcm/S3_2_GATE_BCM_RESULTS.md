# S3.2 — Sigmoid Gate + BCM Ablation

**Date:** 2026-05-10 · RTX 4060 8GB  
**Scripts:** `s3_2_a.py`, `s3_2_b.py` (training scripts; not shipped in this repo)  
**Corpus:** `clean_train_950k.txt` (945K chars, ESL + dialogue)

---

## Why S3.2?

Earlier S3 runs had **Gate% = 100%** (broken magnitude gate). Vision experiments showed **sigmoid gates** working at 17–43% activity. S3.2 moves that to the language model and ablates an extra BCM stream.

---

## Corpus

| Field | Value |
|-------|-------|
| Train | 945,987 characters |
| Format | ~55% dialogue (`A:` / `B:`) |
| Vocab | 85 characters |
| Sequences | 3,695 train / 200 val (len 256) |

Compare only against **Model2-only-clean** on the same 950K corpus — not older 157K ESL runs.

---

## Architecture

| Component | S3.2-A | S3.2-B |
|-----------|--------|--------|
| **Model1** | 3× causal WHT, d=128 | same |
| **Model2** | 4× causal WHT, d=256 | same |
| BCM stream | none | BCMLayer d=128 |
| Gates | 1× SigmoidPacketGate (M1→M2) | 2× (M1 + BCM) |
| Params | ~3.02M | ~3.07M |

```python
gate = sigmoid(Linear(h_M1) - bias)
h_M2 += gate * project(h_M1)
loss += 0.01 * gate.mean()  # sparsity
```

---

## S3.2-A results (15 epochs)

| Ep | PPL-M2 | Gate% |
|----|--------|-------|
| 5 | 5.036 | 18.9% |
| 10 | 4.415 | 18.6% |
| **15** | **4.332** | **18.8%** |

Gate settles: 83% → 52% → 32% → **~19%** by epoch 5.

**Routing:** Highest gates on `\n` tokens (dialogue turn boundaries) — routing is **meaningful**, not random.

---

## S3.2-B results (15 epochs)

| Ep | PPL-M2 | G-M1 | G-BCM |
|----|--------|------|-------|
| 15 | **4.399** | 4.0% | **1.1%** |

Dual sparsity penalties collapse the BCM gate by epoch 2. No PPL win vs S3.2-A.

---

## Fair comparison (950K clean)

| Model | PPL-M2 | Δ vs Model2-only |
|-------|--------|------------------|
| Model2-only-clean | 4.3901 | — |
| **S3.2-A** | **4.332** | **−0.058** |
| S3.2-B | 4.399 | +0.009 |

**Finding B066:** Sigmoid + sparsity → real sparse routing (~18.8%).  
**Finding B067:** Second BCM gate + second sparsity term → BCM path useless on LM.  
**Finding B068:** Turn-boundary (`\n`) routing hypothesis supported.

---

## Related

- [`../MODEL2_ONLY_BASELINE_B069.md`](../MODEL2_ONLY_BASELINE_B069.md)  
- [`../LEAKAGE_ANALYSIS_B076.md`](../LEAKAGE_ANALYSIS_B076.md)  
- [`S3_ABLATION_RESULTS.md`](S3_ABLATION_RESULTS.md)
