# BCM — Language & Hebbian Experiments (Phases 8–15)

## Phases 8–13 — Hebbian + BCM on Turkish data

**B018 — G4 rank collapse:** v2 mean pool rank 29/32; causal attention rank **2/32** (severe low-rank attractor with Hebbian + causal).

| Model | Val accuracy | Effective rank |
|-------|--------------|----------------|
| v2 mean pool + dropout | 85.31% | 29/32 |
| HebbianLMModel | **86.21%** | — |
| CausalHebbian | 85.77% | 2/32 |

**Lesson:** Causal Hebbian path needs rank regularization; mean-pool + dropout is the stable recipe.

---

## Phase 14 — VNE U-shape

**B034–B036:** U-shaped VNE curves are **not WHT-specific** — appear under BCM dynamics on multiple kernels (WHT ~1.19, ReLU ~1.17). Not a reliable standalone phenomenon for model selection.

---

## Phase 15 — BCM Theta DNA (language LM)

| Metric | Value |
|--------|-------|
| Weight shape | (64, 256) |
| Mean θ | **81.4°** |
| Neurons in T-OPT band (>81°) | **75%** (48/64) |
| Effective rank | 54/64 |
| Val PPL @ epoch 4 | **2.74** |

**B038:** BCM thresholds naturally converge toward the T-OPT angular band without special initialization tricks.

---

## Phase 17 — Neuron standard model (BCM anatomy)

**B047 — Collective T-OPT**

- Per-neuron θ: 30–60° band (individual T-OPT weak)
- Global SVD θ: **98.4%** in T-OPT band (collective structure strong)

**B048 — Clone mechanism**

| Corpus | Clones | Unique | SV ratio |
|--------|--------|--------|----------|
| Sentiment BCM | 55/64 (~86%) | 9 | 7.1× |
| TinyStories BCM | 49/64 (~77%) | 15 | 5.0× |
| CIFAR BCM | 0/64 | 64 | 1.6× |

BCM normalization forces unit-norm weights → dominant directions + copied neurons on **narrow text**; vision gradients stay diverse.

**B049 — Corpus drives specialization**

Probe labels (character / causal / temporal neurons) shift with corpus quality — anatomy is data-dependent.

---

## Config notes (from ablations)

- Dropout **0.3** + BCM → strong val (~85%)  
- Weight decay on embeddings hurts rare tokens (B014)  
- Pool-after dropout regularizes without killing signal (B015)
