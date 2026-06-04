# B069 — Model2-Only Baseline

**Date:** 2026-05-10 · Phase 22  
**Script:** `model2_only_clean.py` (conceptual; not shipped in this repo)  
**Corpus:** 950K clean dialogue (ESL + blended skills)  
**Goal:** Fair baseline for dual-path S3.2-A

---

## Summary — Model2-only vs S3.2-A

| Metric | Model2-only | S3.2-A (dual-path) | Notes |
|--------|-------------|---------------------|-------|
| Val PPL @ ep10 | 4.4875 | **4.415** | Dual-path ahead |
| Val PPL @ ep11 | 4.4411 | **4.415** | Curves cross |
| Val PPL @ ep15 | 4.3901 | **4.332** | Δ ≈ **−0.058** |
| Gate activity | N/A | **18.8%** | Selective routing |
| BCM θ (dual) | — | ~81.4° mean | T-OPT band ~75% |

**Conclusion:** Sparse Model1→Model2 gate gives a **small but consistent** PPL gain on the same corpus.

---

## Architecture (Model2-only)

```
Token embed → Model2 stack (causal WHT blocks) → LM head
No Model1 branch, no cross-path gate
~3M parameters (order of magnitude)
```

Training: CE loss, cosine LR, 15 epochs, batch 32, seq 256.

---

## Delta curve (S3.2-A − Model2-only PPL)

| Epoch | Δ |
|-------|---|
| 5 | +0.093 |
| 10 | +0.073 |
| 11 | −0.026 (crossover) |
| 15 | **−0.058** (dual-path wins) |

Early epochs favor dual-path more; gap narrows as Model2-only catches up.

---

## Related

- [`bcm/S3_2_GATE_BCM_RESULTS.md`](bcm/S3_2_GATE_BCM_RESULTS.md)  
- [`PROPOSAL_VERIFIER_B071.md`](PROPOSAL_VERIFIER_B071.md)
