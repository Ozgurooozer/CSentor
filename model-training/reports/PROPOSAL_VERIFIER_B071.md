# B071 — Proposal + Verifier Framework

**Date:** 2026-05-10 · Phase 22  
**Status:** Conceptual design (post S3.2 validation)  
**Context:** Model2-only baseline (B069) + S3.2-A sparse routing evidence

---

## Naming

| Role | Name | Responsibility |
|------|------|----------------|
| Speculative path | **Model1** | Proposes continuations from rich context |
| Causal path | **Model2** | Verifies proposals against past-only context |

Legacy training scripts may still use `Model1` / `Model2` variable names; reports use **Model1 / Model2**.

---

## Architecture shift

### Earlier view (S3.1–S3.2)

```
Model1 (teacher) → SigmoidPacketGate → Model2 (student) → LM head
```

Information flows from Model1 into Model2 (distillation-style).

### B071 view (proposal + verifier)

```
Model1 (speculative generator)
    → candidate continuation
Model2 (causal verifier)
    → accept / reject
    → output or retry / fallback
```

**Difference:** Model1 **proposes**; Model2 **judges** — not blind self-training on raw model output.

---

## Role split

| | Model1 | Model2 |
|--|--------|--------|
| Context | Bidirectional or wider receptive field | Strictly causal (past only) |
| Natural role | Hypothesis / proposal | Consistency check |
| Gate | Sends packets | Filters packets |

---

## Synthetic data rules

1. Never train Model2 on unfiltered Model1 outputs alone.  
2. Require verifier score / threshold before adding to training pool.  
3. Log rejections for audit (same discipline as security “confirm before escalate”).

---

## Link to Wi-Fi product

| Research | Planned field use |
|----------|-------------------|
| Model1 proposes motion pattern | Multi-sensor fusion suggests zone |
| Model2 verifies | Rule + conservative path must agree |
| Filtered synthetic loop | Operator-labelled night events for retrain |

---

## Related

- [`MODEL2_ONLY_BASELINE_B069.md`](MODEL2_ONLY_BASELINE_B069.md)  
- [`bcm/S3_2_GATE_BCM_RESULTS.md`](bcm/S3_2_GATE_BCM_RESULTS.md)  
- [`LEAKAGE_ANALYSIS_B076.md`](LEAKAGE_ANALYSIS_B076.md)
