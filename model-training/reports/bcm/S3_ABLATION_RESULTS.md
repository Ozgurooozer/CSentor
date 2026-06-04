# S3 — Ablation & Control Experiments

**Date:** 2026-05-09  
**Goal:** Does **Model1** carry useful information, or only add capacity?

---

## Dual-path layout (S3-Lite)

```
Input
  ├─ Model1: embed d=128, 3× WHT (non-causal in early leaky runs)
  │     h_M1 ──► SparsePacketGate ──► inject into Model2
  └─ Model2: embed d=256, 4× WHT (causal), LM head
```

**Params:** ~3.03M dual-path vs ~2.48M Model2-only.

---

## S3-Lite reference (157K ESL — leaky era)

| Epoch | PPL-M2 | Gate% |
|-------|--------|-------|
| 10 | 1.05 | 100% |
| 20 | **1.022** | 100% |

Val PPL **&lt; 2.0** → later classified as **leakage / invalid** for honest comparison.  
Gate never sparse (magnitude gate always on).

---

## Experiment 1 — Gate override (frozen weights)

| Forced gate | PPL-M2 |
|-------------|--------|
| 0.0 (off) | 140.77 (Model2 collapses) |
| 0.5 | 1.035 |
| 1.0 | 1.022 |

Model2 **depends** on Model1 injection when trained with gate open.

---

## Experiment 2 — Model2-only control

Honest causal **Model2-only** on same pipeline: PPL **~5.77** (157K corpus) — not ~1.0.

Proves sub-2 PPL was not achievable without leak.

---

## Experiment 3 — Causal Model1

Making Model1 causal removes leak; PPL rises to honest band (~5.5+ on 157K).

---

## Lessons

| Issue | Resolution |
|-------|------------|
| 100% gate | Replaced by sigmoid + sparsity (S3.2) |
| Val PPL ≈ 1 | Leakage heuristic; redesign causal/asymmetric paths |
| Fair baseline | Model2-only-clean on **950K** corpus (B069) |

---

## Related

- [`S3_2_GATE_BCM_RESULTS.md`](S3_2_GATE_BCM_RESULTS.md)  
- [`../LEAKAGE_ANALYSIS_B076.md`](../LEAKAGE_ANALYSIS_B076.md)
