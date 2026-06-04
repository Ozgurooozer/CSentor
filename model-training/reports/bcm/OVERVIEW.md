# BCM (Bienenstock–Cooper–Munro) — Experiment Program

BCM is the plasticity rule used across language models, vision probes, and dual-path WHT-LM runs in this research line. This folder collects **BCM-focused** experiment write-ups.

**Hardware:** RTX 4060 8GB · PyTorch

---

## What BCM does here

- **Sliding threshold** `θ` per neuron — fires when post-synaptic activity exceeds its threshold.
- **Normalization** `W / ||W||` — pushes weights onto a unit sphere; often yields **clone neurons** (high cosine similarity) on narrow text corpora.
- **T-OPT angle band** — collective convergence around ~81° on language BCM (B038); per-neuron angles vary on vision.

---

## Experiment map

| ID / file | Domain | Main result |
|-----------|--------|-------------|
| **B038** | Language BCM θ DNA | Mean θ ≈ 81.4°, ~75% neurons in T-OPT band; val PPL ~2.74 @ ep4 |
| **B039–B044** | Visual BCM | Patch tokens required; Gabor-like filters ~62–81% on CIFAR; row-wise input → saturation |
| **B045** | WHT ablation | WHT + BCM + sparse attn vs ReLU — WHT **56×** better PPL @ ep1 |
| **B048** | Clone analysis | Text BCM ~77–86% clone; **CIFAR BCM 0% clone**, 64 distinct filters |
| **B049** | Corpus vs anatomy | Corpus quality changes neuron specialization (character / causal / temporal) |
| **S3.2-B** | Dual-path + BCM stream | [`S3_2_GATE_BCM_RESULTS.md`](S3_2_GATE_BCM_RESULTS.md) — G-BCM ~1.1%; no gain vs S3.2-A |
| **Vision gate** | CIFAR + sigmoid gate | Gate-BCM 0.17–0.43 selective (unlike broken magnitude gate in S3) |

---

## Language + Hebbian (phases 8–15)

| Model | Val | Rank | Note |
|-------|-----|------|------|
| v2 mean pool + dropout | 85.31% | 29/32 | Stable baseline |
| HebbianLMModel | 86.21% | — | Best val in series |
| CausalHebbian | 85.77% | **2/32** | Low-rank attractor with causal attn |

**Takeaway:** BCM + causal attention can collapse effective rank; dropout + mean pool safer for classification.

Details: [`LANGUAGE_HEBBIAN.md`](LANGUAGE_HEBBIAN.md)

---

## Vision + CIFAR (phases 16–19)

| Finding | Detail |
|---------|--------|
| Patch 7×7 | BCM learns diverse filters; row-only → all neurons saturated |
| Freeze after epoch 3 | Rank ~58/64 stable; theta ~67.9° — no late collapse |
| Alignment gap | Frozen BCM + moving encoder → linear probe drops (~12.9%) |
| CIFAR-100 profile | Rank ~102/128, Gabor ~80%, clone ~0% |

Details: [`VISUAL_CIFAR.md`](VISUAL_CIFAR.md)

---

## Dual-path language (S3.2)

| Variant | PPL-Model2 (950K clean) | Gate behavior |
|---------|----------------------|---------------|
| Model2-only-clean | 4.390 | — |
| **S3.2-A** (no BCM) | **4.332** | Model1 gate ~18.8%, `\n` routing |
| S3.2-B (+ BCM stream) | 4.399 | G-BCM ~1.1% — **dual sparsity collapses BCM path** |

**Takeaway:** Sparse sigmoid routing on Model1→Model2 works; adding a second BCM gate stream over-regularizes.

Full tables: [`S3_2_GATE_BCM_RESULTS.md`](S3_2_GATE_BCM_RESULTS.md)

---

## WHT × BCM (B045)

Same BCM + sparse attention; only token mixer changes:

| Mixer | Val PPL @ ep1 | Best val PPL |
|-------|---------------|--------------|
| **WHT** | 6.05 | **3.19** (ep7) |
| ReLU | 343.93 | ~249 (still failing) |

BCM alone does not explain success — **global token mixing (WHT) is necessary**.

Full report: [`WHT_ABLATION_B045.md`](WHT_ABLATION_B045.md)

---

## Neuron clones (no physics analogies)

High cosine pairs on text BCM are **documented as redundancy / PCA-like compression**, not as external physics metaphors.

| Corpus | Clone ratio | Unique neurons |
|--------|-------------|----------------|
| Sentiment | ~86% (55/64) | 9 |
| TinyStories | ~77% (49/64) | 15 |
| CIFAR BCM | **0%** | 64 |

Details: [`NEURON_CLONES.md`](NEURON_CLONES.md)

---

## Open BCM issues

1. **Encoder–BCM alignment** under encoder freeze  
2. **Dual gate + dual sparsity** — S3.2-B BCM path useless  
3. **Larger honest corpus** for language BCM diversity  
4. **Hierarchical BCM** (V1→V2) — planned after alignment fix  

---

## Files in this folder

| File | Content |
|------|---------|
| `OVERVIEW.md` | This index |
| `LANGUAGE_HEBBIAN.md` | Text classification + Hebbian rank |
| `VISUAL_CIFAR.md` | Patch BCM, Gabor, freeze |
| `NEURON_CLONES.md` | B048-style clone stats |
| `WHT_ABLATION_B045.md` | WHT vs ReLU with shared BCM |
| `S3_2_GATE_BCM_RESULTS.md` | S3.2-A/B including BCM ablation |
| `S3_ABLATION_RESULTS.md` | S3 series ablations |
