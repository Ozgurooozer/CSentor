# WHT-LM — technical overview

**Status:** Active research  
**Goal:** Efficient sequence modeling with WHT mixing, BCM, and **Model1 / Model2** dual paths.

**Hardware:** RTX 4060 8GB · PyTorch

---

## Components

### A — Turkish classifier (done)

~85% validation on ~217K samples.

### B — WHT-LM (in progress)

Next-token LM with **Walsh–Hadamard** mixing — O(n log n) vs O(n²) attention.

### C — Dual path

| Path | Role |
|------|------|
| **Model1** | Proposal / rich-context stream |
| **Model2** | Causal verifier / generator |
| **SigmoidPacketGate** | Sparse Model1 → Model2 routing (~18.8% on clean run) |

---

## Validated findings

| Finding | Evidence |
|---------|----------|
| WHT vs transformer | ~1 PPL better, ~6× lower gradient norm |
| WHT + BCM required | B045: 56× PPL without WHT |
| Leakage heuristic | Val PPL &lt; 2 → invalid |
| Honest asymmetric run | Option A PPL ~3.51 |
| Visual BCM | ~80% Gabor, 0% clones on CIFAR |

---

## Report index

See [`reports/INDEX.md`](reports/INDEX.md) and [`reports/bcm/OVERVIEW.md`](reports/bcm/OVERVIEW.md).
