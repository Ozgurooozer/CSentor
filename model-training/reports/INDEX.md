# Model training — report index

**Project:** WHT-LM dual-path research · **Hardware:** RTX 4060 8GB  
**Naming:** dual paths are **Model1** (proposal / wide context) and **Model2** (causal verifier / generator).

---

## Start here

| Document | Description |
|----------|-------------|
| [`../README.md`](../README.md) | Folder entry |
| [`../TECHNICAL_OVERVIEW.md`](../TECHNICAL_OVERVIEW.md) | WHT, BCM, dual-path overview |
| [`FINDINGS_SUMMARY.md`](FINDINGS_SUMMARY.md) | All phases — executive summary |
| [`bcm/OVERVIEW.md`](bcm/OVERVIEW.md) | BCM experiment index |

---

## Core reports (English)

| ID | File | Topic |
|----|------|--------|
| — | [`S1_WHT_ABLATION.md`](S1_WHT_ABLATION.md) | WHT vs transformer |
| B045 | [`bcm/WHT_ABLATION_B045.md`](bcm/WHT_ABLATION_B045.md) | WHT vs ReLU + BCM |
| B069 | [`MODEL2_ONLY_BASELINE_B069.md`](MODEL2_ONLY_BASELINE_B069.md) | Model2-only vs S3.2-A |
| B071 | [`PROPOSAL_VERIFIER_B071.md`](PROPOSAL_VERIFIER_B071.md) | Proposal + verifier framing |
| B076 | [`LEAKAGE_ANALYSIS_B076.md`](LEAKAGE_ANALYSIS_B076.md) | Validation leakage |
| S3 | [`bcm/S3_ABLATION_RESULTS.md`](bcm/S3_ABLATION_RESULTS.md) | Early S3 ablations |
| S3.2 | [`bcm/S3_2_GATE_BCM_RESULTS.md`](bcm/S3_2_GATE_BCM_RESULTS.md) | Sigmoid gate + BCM ablation |

---

## BCM subfolder

| File | Topic |
|------|--------|
| [`bcm/LANGUAGE_HEBBIAN.md`](bcm/LANGUAGE_HEBBIAN.md) | Text / Hebbian / θ profile |
| [`bcm/VISUAL_CIFAR.md`](bcm/VISUAL_CIFAR.md) | Vision / CIFAR / Gabor |
| [`bcm/NEURON_CLONES.md`](bcm/NEURON_CLONES.md) | Clone statistics |

---

## Phase map (short)

| Phase | Focus | Status |
|-------|--------|--------|
| 1–7 | Turkish classification | Archived |
| 8–15 | Hebbian + BCM language | Done |
| 16–19 | Visual BCM | Done |
| 20–25 | Dual-path Model1/Model2 + gates | Done |

Details: [`FINDINGS_SUMMARY.md`](FINDINGS_SUMMARY.md).
