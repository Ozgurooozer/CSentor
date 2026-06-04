# Experimental findings — summary

**Stack:** WHT-LM + BCM + dual-path (**Model1** / **Model2**)  
**Hardware:** RTX 4060 8GB  
**Last update:** 2026-05-11 (through phase 25)

---

## Phase overview

| Phase | Topic | Best result | Status |
|-------|--------|-------------|--------|
| 1–7 | Turkish classification | 98.94% | Archived |
| 8–13 | Hebbian + BCM | 86.21% val | Done |
| 14 | WHT / VNE ablation | U-shape not kernel-specific | Done |
| 15 | BCM θ DNA | θ ≈ 81.4°, 75% T-OPT band | Done |
| 16–19 | Visual BCM | ~81% Gabor, rank 102/128 | Done |
| 20–22 | Dual-path + sigmoid gate | Gate **18.8%**, `\n` routing | Done |
| 23 | Asymmetric causal Option A | PPL **3.51**, Δ **−0.88** vs Model2-only | Done |
| 23-B070 | Model1 freeze | Δ **+0.012** PPL | Done |
| 24 | 3-way compare | PPL ≈ 1.009 → corpus issue | Done |
| 25 | Role split | Model1 router, Model2 modeler | Done |

---

## Proven findings

| Finding | Evidence |
|---------|----------|
| Leakage flag (val PPL &lt; 2) | Leaky S3.2 / S4 runs |
| WHT vs transformer | ~1.0 PPL better, ~6× stabler gradients |
| WHT required (B045) | 56× PPL gap vs ReLU @ ep1 |
| Selective gate (S3.2-A) | **18.8%** gate, `\n` peaks |
| Model1 helps on clean corpus | 4.332 vs 4.390 Model2-only (Δ −0.058) |
| BCM second gate fails (S3.2-B) | G-BCM ~1.1%, no PPL gain |
| Model1 freeze reusable | B070 Δ +0.012 |
| Distillation Option B failed | +2.21 vs Model2-only |

---

## Open issues

1. Encoder–BCM alignment under freeze  
2. Larger honest corpus (5M+ chars)  
3. High gate% on Option A (~99%) vs selective S3.2-A (~19%)  
4. Turn-boundary handling for dialogue `\n`  

---

## Report links

| File | Focus |
|------|--------|
| [`INDEX.md`](INDEX.md) | Full catalog |
| [`../TECHNICAL_OVERVIEW.md`](../TECHNICAL_OVERVIEW.md) | Technical overview |
| [`bcm/OVERVIEW.md`](bcm/OVERVIEW.md) | BCM program |
| [`LEAKAGE_ANALYSIS_B076.md`](LEAKAGE_ANALYSIS_B076.md) | Leakage |
| [`PROPOSAL_VERIFIER_B071.md`](PROPOSAL_VERIFIER_B071.md) | Proposal + verifier |
| [`MODEL2_ONLY_BASELINE_B069.md`](MODEL2_ONLY_BASELINE_B069.md) | Model2-only baseline |
| [`bcm/S3_2_GATE_BCM_RESULTS.md`](bcm/S3_2_GATE_BCM_RESULTS.md) | S3.2 results |
