# Model training

Documentation for the **WHT-LM** research line: WHT mixing, **BCM** plasticity, and dual-path **Model1** (proposal) + **Model2** (causal verifier). Supports the hybrid ML layer described in [`../PORTFOLIO.md`](../PORTFOLIO.md).

Part of a **Tremium Software job application prototype** (see root [`README.md`](../README.md)). Training scripts referenced in reports use neutral filenames; legacy code may still use older path aliases.

---

## Start here

| Document | Purpose |
|----------|---------|
| [`TECHNICAL_OVERVIEW.md`](TECHNICAL_OVERVIEW.md) | Stack overview |
| [`reports/FINDINGS_SUMMARY.md`](reports/FINDINGS_SUMMARY.md) | Phase summary |
| [`reports/INDEX.md`](reports/INDEX.md) | Report catalog |
| [`reports/bcm/OVERVIEW.md`](reports/bcm/OVERVIEW.md) | BCM experiments |

---

## Key results

| Result | Report |
|--------|--------|
| WHT beats matched transformer | [`reports/S1_WHT_ABLATION.md`](reports/S1_WHT_ABLATION.md) |
| WHT vs ReLU (56× @ ep1) | [`reports/bcm/WHT_ABLATION_B045.md`](reports/bcm/WHT_ABLATION_B045.md) |
| Leakage (PPL &lt; 2) | [`reports/LEAKAGE_ANALYSIS_B076.md`](reports/LEAKAGE_ANALYSIS_B076.md) |
| Model2-only vs dual-path | [`reports/MODEL2_ONLY_BASELINE_B069.md`](reports/MODEL2_ONLY_BASELINE_B069.md) |
| S3.2 gate + BCM | [`reports/bcm/S3_2_GATE_BCM_RESULTS.md`](reports/bcm/S3_2_GATE_BCM_RESULTS.md) |
| Proposal + verifier | [`reports/PROPOSAL_VERIFIER_B071.md`](reports/PROPOSAL_VERIFIER_B071.md) |

---

## BCM folder

[`reports/bcm/`](reports/bcm/) — language Hebbian, visual CIFAR, neuron clones, S3 ablations.

---

## Hardware

NVIDIA RTX 4060 8GB · PyTorch
