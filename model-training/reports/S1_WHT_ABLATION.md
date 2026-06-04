# S1 — WHT vs Transformer Ablation

**Date:** 2026-05-09 · RTX 4060  
**Corpus:** ~477K characters, vocab 55

---

## B065 — Curriculum WHT test

Compare three setups (~667K params, 20 epochs):

| Model | Val PPL @ ep20 | vs Transformer |
|-------|----------------|----------------|
| A — Transformer baseline | 2.04 | ref |
| B — WHT-LM | **1.02** | **−1.02** |
| C — Curriculum WHT (non-causal → causal) | 1.02 | ~0 vs B |

| Metric | Result |
|--------|--------|
| WHT vs Transformer (Δ PPL) | **−1.02** (WHT better) |
| Avg gradient norm | WHT **0.10** vs Transformer **0.62** (~6× stabler) |
| Curriculum extra gain | Negligible on this small set |

**Finding:** WHT mixer beats matched transformer on PPL with far stabler gradients. Curriculum did not help at this data scale.

---

## Hierarchical BCM variant

| Model | Best PPL | Params |
|-------|----------|--------|
| WHT-LM baseline | 1.082 | 662K |
| Hierarchical BCM | **1.067** | 246K |

BCM hierarchical variant slightly beats baseline with **~63% fewer** backbone params; effective rank PR ≈ 51 (no collapse in this setup).

---

## Related

- [`bcm/WHT_ABLATION_B045.md`](bcm/WHT_ABLATION_B045.md) — WHT vs ReLU with shared BCM  
- [`FINDINGS_SUMMARY.md`](FINDINGS_SUMMARY.md)
