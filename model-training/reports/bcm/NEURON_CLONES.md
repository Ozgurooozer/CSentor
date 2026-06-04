# BCM Neuron Clones & Diversity (B048, B047)

## Question

Does BCM learn many independent features, or collapse to redundant “clone” neurons?

---

## Method

- Track pairwise cosine similarity of BCM weight rows after training  
- **Clone:** cosine > 0.99 (near-duplicate direction)  
- Compare corpora: sentiment text, TinyStories, CIFAR patches  

---

## Results

| Corpus | Clones | Unique | Dominant SV ratio |
|--------|--------|--------|-------------------|
| Sentiment BCM | 55/64 (**86%**) | 9 | 7.1× |
| TinyStories BCM | 49/64 (**77%**) | 15 | 5.0× |
| **CIFAR BCM** | **0/64** | **64** | 1.6× |

---

## Interpretation

1. **Normalization** `W ← W/||W||` concentrates mass on a few dominant directions on **low-diversity text**.  
2. Remaining neurons **copy** those directions → high clone %.  
3. **Rich visual gradients** spread activations → **no clones**, full 64-filter bank.  
4. This is **expected BCM geometry**, not a random training bug.

---

## Collective vs per-neuron T-OPT (B047)

| Measure | Observation |
|---------|-------------|
| Per-neuron θ | Mostly 30–60° |
| Global SVD of BCM block | **98.4%** in T-OPT band |

Single neurons need not sit at T-OPT individually; the **ensemble** can still show T-OPT structure.

---

## Engineering implications

| Use case | Recommendation |
|----------|----------------|
| Text LM BCM | Expect clones; widen corpus or add diversity penalty |
| Vision BCM | Clone rate low; suitable for filter-bank interpretation |
| Dual-path S3.2-B | Second BCM gate + sparsity → path collapses — see S3.2 report |

---

## Related files

- [`OVERVIEW.md`](OVERVIEW.md)  
- [`LANGUAGE_HEBBIAN.md`](LANGUAGE_HEBBIAN.md)  
- [`VISUAL_CIFAR.md`](VISUAL_CIFAR.md)
