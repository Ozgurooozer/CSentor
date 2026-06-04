# B076 — Leakage Analysis & Fixes

**Date:** 2026-05-10  
**Issue:** S3.2-A validation PPL ≈ **1.0086** (suspiciously low)  
**Root cause:** Model1 **non-causal** → sees future tokens → leaks into Model2 via gate  
**Symptom:** Gate **90%+** active (vs ~18.8% on fixed clean run)

---

## Observed anomaly

| Run | Val PPL | Gate % | Assessment |
|-----|---------|--------|------------|
| Leaky S3.2-A | 1.0086 | 90%+ | Invalid — future information |
| Expected | ~4.3 | ~18–20% | Match Model2-only baseline band |
| Honest Option A | 3.5084 | high but PPL honest | Causal both paths |

**Heuristic (kept in production ML):** validation PPL **&lt; 2.0** on dialogue LM → treat as **leakage** until proven otherwise.

---

## Root cause

1. **Model1 `causal=False`** in `WHTGatedBlock` — full sequence visible.  
2. **SigmoidPacketGate** uses only Model1 hidden state — no consistency check with Model2.  
3. High gate → Model2 receives future-aware hints → trivial prediction.

```python
# Leaky pattern (conceptual)
gate = sigmoid(linear(h_model1) - bias)
h_model2 += gate * project(h_model1)
```

---

## Fix options

| Option | Change | Pros | Cons |
|--------|--------|------|------|
| **A** | Model1 `causal=True` | No leak, simple | Less asymmetry |
| **B** | Asymmetric sizes (large Model1, small Model2) + both causal | Honest PPL ~3.51, Δ −0.88 vs Model2-only | Gate % can stay high |
| **C** | Shift + mask tricks alone | Partial | Insufficient alone (documented) |

**Adopted path:** Option A/B family — both paths causal for honest metrics; asymmetric capacity for Model1 contribution.

---

## Expected metrics after fix

- Val PPL **&gt; 2.0** (typically ~3.5–4.4 on clean 950K corpus)  
- Train ≈ Val (no magic gap)  
- Gate stabilizes ~15–20% with sparsity loss on clean run  

---

## Related

- [`S3_ABLATION_RESULTS.md`](S3_ABLATION_RESULTS.md)  
- [`bcm/S3_2_GATE_BCM_RESULTS.md`](bcm/S3_2_GATE_BCM_RESULTS.md)  
- [`MODEL2_ONLY_BASELINE_B069.md`](MODEL2_ONLY_BASELINE_B069.md)
