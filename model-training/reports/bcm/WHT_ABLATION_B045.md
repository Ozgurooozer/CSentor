# B045 — WHT vs ReLU Token Mixer (shared BCM stack)

**Date:** 2026-05-07 · `wht_ablation_lm.py`

---

## Question

```
WHT + BCM + sparse attention  →  PPL ?
ReLU + BCM + sparse attention →  PPL ?
```

If only WHT wins → WHT is the critical mixer (not BCM alone).

---

## Setup

**Single variable:** pre-mixer (WHT butterfly vs ReLU MLP).  
**Fixed:** BCM layer, sparse attention k=4, FFN 512, vocab 34K, batch 32, seq 64, LR 3e-4, seed 42, 10 epochs.

---

## Val PPL

| Epoch | WHT | ReLU | Ratio |
|-------|-----|------|-------|
| 1 | **6.05** | 343.93 | **56×** |
| 3 | 3.65 | 248.68 | 68× |
| 7 | **3.19** (best) | — | — |

ReLU path **does not learn** on this setup; WHT provides global token mixing ReLU block lacks.

---

## Conclusion

BCM + attention alone are **not** sufficient — **WHT mixing is required** for workable LM PPL.

---

## Related

- [`../S1_WHT_ABLATION.md`](../S1_WHT_ABLATION.md)  
- [`OVERVIEW.md`](OVERVIEW.md)
