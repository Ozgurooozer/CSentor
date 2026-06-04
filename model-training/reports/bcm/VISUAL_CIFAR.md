# BCM — Vision & CIFAR Experiments (Phases 16–19)

## B039 — Saturation on row-wise input

Row-based BCM: all neurons sit on **tanh saturation**.  
Cause: pre-activation std ≈ 2.59 → θ rises quickly → neurons shut off.

---

## B040 — Patch tokenization (critical)

| Input layout | BCM behavior |
|--------------|--------------|
| **7×7 patches** | Diverse filters, BCM learns |
| Row-wise flatten | Saturated, no diversity |

---

## B041–B042 — Theta & hyperactivation

- After epoch ~12, θ drifts up (3.55 → 6.21) → neurons suppressed  
- Cosine LR schedule can fight BCM θ updates  
- Root: `y_pre` std ≈ 2.59, `y_pre²` mean ≈ 6.73 → θ tracks hyperactivation → collapse risk

---

## B043–B044 — Gabor & V1-like behavior

| Dataset | Linear probe | Interpretation |
|---------|--------------|----------------|
| CIFAR | ~20.1% | Edges / frequency, not concepts |
| MNIST | ~86.8% | Simpler structure |

**~62.5–81%** of BCM filters Gabor-like on natural images — consistent with V1-style edge learning.

Hierarchy (V2/IT) still required for object-level semantics.

---

## CIFAR masked autoencoder — B045 freeze protocol

```
Epochs 1–3: rank 58–59, Gabor 48–52/64  ← BCM learning
Epoch 4+:   rank 58 stable, θ ≈ 67.9°   ← freeze_after=3, no collapse
```

**B046 — Alignment problem**

- Freeze BCM → encoder drifts away → linear probe **~12.9%**  
- Keep BCM live → θ explosion / collapse  
- **Open:** co-train encoder + BCM with coupled θ schedule

---

## CIFAR-100 BCM profile (Phases 18–19)

| Metric | Result |
|--------|--------|
| Effective rank | ~102/128 |
| Clone rate | **0%** |
| Gabor-like filters | **~80%** |

Vision BCM stays **diverse** vs language BCM clones.

---

## Vision + sigmoid gate (experiments A / B / C)

Magnitude threshold gate in early S3: **Gate% ≈ 100%** (broken selectivity).  
Sigmoid gate on vision BCM path: **0.17–0.43** — selective, usable.

This motivated **S3.2-A** language model with `SigmoidPacketGate` (see [`S3_2_GATE_BCM_RESULTS.md`](S3_2_GATE_BCM_RESULTS.md)).
