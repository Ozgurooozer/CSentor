"""Statistics and signal processing utilities for WiFi motion detection."""

import math
from typing import List, Tuple


# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

# Minimum number of samples required for variance / peak-to-peak calculation
_MIN_SAMPLES: int = 2

# Minimum number of samples required for frequency analysis (DFT)
_MIN_SAMPLES_FREQ: int = 16

# Frequency band limits for dominant frequency search (Hz)
_FREQ_MIN_HZ: float = 0.05
_FREQ_MAX_HZ: float = 3.5

# Default sampling rate for WiFi CSI amplitude data (Hz)
_DEFAULT_SAMPLE_HZ: float = 1.9

# Frequency thresholds for motion-type classification (Hz)
#   Sakin (still)         <= 0.15  – no detectable body motion
#   Nefes~ (breathing)    <= 0.5   – chest movement from respiration
#   Kucuk hrt (small)     <= 1.2   – slight limb / body part motion
#   Yurume~ (walking)     <= 2.5   – full body walking pace
#   Hizli hrt (fast)       > 2.5   – running / rapid motion
_FREQ_THRESHOLD_SAKIN: float = 0.15
_FREQ_THRESHOLD_NEFES: float = 0.5
_FREQ_THRESHOLD_KUCUK_HRT: float = 1.2
_FREQ_THRESHOLD_YURUME: float = 2.5

# Variance thresholds for motion-magnitude classification
# Derived empirically from WiFi CSI amplitude variance on real captures.
_VAR_THRESHOLD_COK_KUCUK: float = 3.0
_VAR_THRESHOLD_KUCUK: float = 10.0
_VAR_THRESHOLD_ORTA: float = 30.0
_VAR_THRESHOLD_BUYUK: float = 100.0

# Baseline sensitivity parameters at reference level = 1.0
_BASE_MULT: float = 6.0
_BASE_MIN_VAR: float = 25.0
_BASE_MIN_DEL: float = 8.0
_BASE_MIN_PTP: float = 12.0

# Per-level adjustment slopes applied when level > 1
_SLOPE_MULT: float = 0.35
_SLOPE_MIN_VAR: float = 1.6
_SLOPE_MIN_DEL: float = 0.45
_SLOPE_MIN_PTP: float = 0.7

# Lower clamping bounds for sensitivity parameters
_CLAMP_MULT: float = 0.05
_CLAMP_PARAM: float = 0.1

# Signal-quality label thresholds (dBm)
_SIGNAL_MUKEMEL: int = -50
_SIGNAL_COK_IYI: int = -60
_SIGNAL_IYI: int = -70
_SIGNAL_ORTA: int = -80
_SIGNAL_ZAYIF: int = -90

# RSSI clamping range for the signal-quality bar display
_RSSI_MIN: int = -100
_RSSI_MAX: int = -30
_RSSI_RANGE: int = _RSSI_MAX - _RSSI_MIN  # 70

# Number of character-width "bars" in the signal-quality visualisation
_NUM_BARS: int = 10


def dbm_to_mw(dbm: float | int) -> float:
    """Convert dBm to milliwatts (linear power)."""
    return 10.0 ** (dbm / 10.0)


def mw_to_dbm(mw: float) -> float:
    """Convert milliwatts back to dBm."""
    if mw <= 0:
        return -100.0
    return 10.0 * math.log10(mw)


def variance_mw(data: List[float | int], ddof: int = 1) -> float:
    """Compute variance in linear power domain (mW), not in dBm.

    RSSI in dBm is logarithmic. Statistical operations like variance
    should be done in linear power (mW) to be physically meaningful.
    A 3 dB change at -30 dBm and -80 dBm have the same dB value but
    vastly different mW values.
    """
    if len(data) < 2:
        return 0.0
    mw_vals = [dbm_to_mw(x) for x in data]
    mean = sum(mw_vals) / len(mw_vals)
    return sum((x - mean) ** 2 for x in mw_vals) / (len(mw_vals) - ddof)


def variance(data: List[float | int], ddof: int = 1, linear_power: bool = False) -> float:
    """Compute the (sample) variance of a dataset.

    Uses Bessel's correction (n - ddof) in the denominator.  With the default
    ddof=1 this returns the unbiased sample variance; pass ddof=0 to obtain
    the population variance instead.

    When ``linear_power=True`` the data is first converted from dBm to
    milliwatts so that variance is computed in the linear power domain,
    which is physically meaningful for logarithmic RSSI measurements.

    Args:
        data: List of numeric samples.
        ddof: Delta degrees of freedom.  Defaults to 1 (sample variance).
        linear_power: If True, convert dBm to mW before computing variance.

    Returns:
        The variance, or 0.0 if fewer than _MIN_SAMPLES samples are provided.
    """
    if len(data) < _MIN_SAMPLES:
        return 0.0
    if linear_power:
        mw_vals = [dbm_to_mw(x) for x in data]
        mean = sum(mw_vals) / len(mw_vals)
        return sum((x - mean) ** 2 for x in mw_vals) / (len(mw_vals) - ddof)
    mean = sum(data) / len(data)
    return sum((x - mean) ** 2 for x in data) / (len(data) - ddof)


def peak_to_peak(data: List[float | int]) -> float:
    """Compute the peak-to-peak amplitude (max - min) of a dataset.

    Args:
        data: List of numeric samples.

    Returns:
        The difference between the maximum and minimum value, or 0.0 if
        fewer than _MIN_SAMPLES samples are provided.
    """
    if len(data) < _MIN_SAMPLES:
        return 0.0
    return float(max(data) - min(data))


def zero_crossing_rate(data: List[float | int]) -> float:
    """Compute the zero-crossing rate (ZCR) of the mean-centered signal.

    ZCR measures how often the signal crosses its mean. Periodic motion
    (walking, breathing) produces a characteristic ZCR. High ZCR with
    low variance suggests noise; low ZCR with high variance suggests
    sustained motion.
    """
    if len(data) < 3:
        return 0.0
    mean = sum(data) / len(data)
    centered = [x - mean for x in data]
    crossings = sum(
        1 for i in range(1, len(centered))
        if (centered[i-1] >= 0) != (centered[i] >= 0)
    )
    return crossings / (len(data) - 1)


def skewness(data: List[float | int], ddof: int = 1) -> float:
    """Compute sample skewness of the data.

    Motion shifts the RSSI distribution asymmetrically; skewness
    captures the direction and magnitude of this shift.
    """
    if len(data) < 3:
        return 0.0
    n = len(data)
    mean = sum(data) / n
    m2 = sum((x - mean) ** 2 for x in data) / (n - ddof)
    if m2 == 0:
        return 0.0
    m3 = sum((x - mean) ** 3 for x in data) / (n - ddof)
    return m3 / (m2 ** 1.5)


def kurtosis(data: List[float | int], ddof: int = 1) -> float:
    """Compute excess kurtosis of the data (normal distribution = 0).

    Stillness -> low kurtosis (flat noise distribution).
    Motion -> heavy tails (high kurtosis) from occasional large deviations.
    """
    if len(data) < 4:
        return 0.0
    n = len(data)
    mean = sum(data) / n
    m2 = sum((x - mean) ** 2 for x in data) / (n - ddof)
    if m2 == 0:
        return 0.0
    m4 = sum((x - mean) ** 4 for x in data) / (n - ddof)
    return m4 / (m2 ** 2) - 3.0


def dominant_freq(
    samples: List[float | int], sample_hz: float
) -> Tuple[float, float]:
    """Find the dominant frequency and its power in a time-domain signal.

    A brute-force search over discrete Fourier bins (k = 1 .. n/2) is
    performed, limited to frequencies within [_FREQ_MIN_HZ, _FREQ_MAX_HZ].
    The bin with the largest squared-magnitude (power) is returned.

    The DC component is removed by subtracting the mean before the DFT.

    Args:
        samples: Time-domain sample values.
        sample_hz: Sampling rate in Hz.

    Returns:
        A tuple (frequency_hz, power) where power is the unnormalised
        squared magnitude of the Fourier coefficient.  Returns (0.0, 0.0)
        when fewer than _MIN_SAMPLES_FREQ samples are provided.
    """
    n = len(samples)
    if n < _MIN_SAMPLES_FREQ:
        return 0.0, 0.0

    mean = sum(samples) / n
    x = [v - mean for v in samples]

    best_p, best_f = 0.0, 0.0
    for k in range(1, n // 2 + 1):
        freq = k * sample_hz / n
        if freq < _FREQ_MIN_HZ or freq > _FREQ_MAX_HZ:
            continue
        re = sum(x[i] * math.cos(2 * math.pi * k * i / n) for i in range(n))
        im = sum(x[i] * math.sin(2 * math.pi * k * i / n) for i in range(n))
        p = re * re + im * im
        if p > best_p:
            best_p, best_f = p, freq

    return best_f, best_p


def freq_label(hz: float) -> str:
    """Map a frequency value to a human-readable motion label (Turkish).

    Thresholds correspond to typical human motion frequency bands observed
    through WiFi CSI:

        Band            Hz range        Label
        still           <= 0.15         Sakin
        breathing       <= 0.5          Nefes~
        small motion    <= 1.2          Kucuk hrt
        walking         <= 2.5          Yurume~
        fast motion      > 2.5          Hizli hrt

    Note: These labels assume an adequate sampling rate (at least ~2x the
    highest frequency of interest). The actual maximum resolvable DFT bin
    depends on both the sample count and the real-world sampling rate.

    Args:
        hz: Frequency in Hertz (0 or negative means no motion).

    Returns:
        A Turkish-language label string (empty string for no motion).
    """
    if hz <= 0:
        return ""
    if hz < _FREQ_THRESHOLD_SAKIN:
        return "Sakin"
    if hz < _FREQ_THRESHOLD_NEFES:
        return "Nefes~"
    if hz < _FREQ_THRESHOLD_KUCUK_HRT:
        return "Kucuk hrt"
    if hz < _FREQ_THRESHOLD_YURUME:
        return "Yurume~"
    return "Hizli hrt"


def motion_magnitude(var: float) -> str:
    """Map a variance value to a human-readable motion magnitude label.

    Thresholds are derived empirically from WiFi CSI amplitude variance:

        Variance range      Label
        < 3                 Cok Kucuk (very small)
        < 10                Kucuk (small)
        < 30                Orta (medium)
        < 100               Buyuk (large)
        >= 100              Cok Buyuk (very large)

    Args:
        var: Variance of the signal amplitude.

    Returns:
        A Turkish-language magnitude label string.
    """
    if var < _VAR_THRESHOLD_COK_KUCUK:
        return "Cok Kucuk"
    if var < _VAR_THRESHOLD_KUCUK:
        return "Kucuk"
    if var < _VAR_THRESHOLD_ORTA:
        return "Orta"
    if var < _VAR_THRESHOLD_BUYUK:
        return "Buyuk"
    return "Cok Buyuk"


def sensitivity_params(level: float) -> Tuple[float, float, float, float]:
    """Compute sensitivity parameters for motion-detection thresholds.

    Each parameter is linearly interpolated from a baseline value using a
    per-level slope:

        mult    = max(0.05, BASE_MULT    - (eff-1) * SLOPE_MULT    [* scale])
        min_var = max(0.10, BASE_MIN_VAR - (eff-1) * SLOPE_MIN_VAR [* scale])
        min_del = max(0.10, BASE_MIN_DEL - (eff-1) * SLOPE_MIN_DEL [* scale])
        min_ptp = max(0.10, BASE_MIN_PTP - (eff-1) * SLOPE_MIN_PTP [* scale])

    where eff = max(1, level) and scale = level when level < 1, else 1.

    At level=1 the parameters equal the BASE_* constants.  Increasing the
    level reduces each parameter (making detection *more* sensitive).  When
    level < 1 the result is additionally scaled down by level, making the
    detector less sensitive.

    Args:
        level: Sensitivity level (>= 0).  1.0 is the reference baseline.

    Returns:
        A tuple (mult, min_var, min_del, min_ptp) with floor values applied.
    """
    eff = max(1.0, float(level))

    mult = _BASE_MULT - (eff - 1) * _SLOPE_MULT
    min_var = _BASE_MIN_VAR - (eff - 1) * _SLOPE_MIN_VAR
    min_del = _BASE_MIN_DEL - (eff - 1) * _SLOPE_MIN_DEL
    min_ptp = _BASE_MIN_PTP - (eff - 1) * _SLOPE_MIN_PTP

    if level < 1.0:
        scale = float(level)
        mult *= scale
        min_var *= scale
        min_del *= scale
        min_ptp *= scale

    return (
        max(_CLAMP_MULT, mult),
        max(_CLAMP_PARAM, min_var),
        max(_CLAMP_PARAM, min_del),
        max(_CLAMP_PARAM, min_ptp),
    )


def estimate_sampling_rate(timestamps: List[float]) -> float:
    """Estimate the actual sampling rate from a list of timestamps (seconds).

    Returns the reciprocal of the median interval, which is robust
    against occasional outliers (e.g., netsh timeout).
    """
    if len(timestamps) < 2:
        return 1.0
    intervals = [timestamps[i+1] - timestamps[i] for i in range(len(timestamps) - 1)]
    if not intervals:
        return 1.0
    intervals.sort()
    median = intervals[len(intervals) // 2]
    return 1.0 / median if median > 0 else 1.0


def signal_quality(dbm: int | None) -> Tuple[str, str]:
    """Return a label and text-based bar visual for WiFi signal strength.

    The RSSI value is clamped to [_RSSI_MIN, _RSSI_MAX] and scaled to
    _NUM_BARS filled blocks for a progress-bar style display.

    Args:
        dbm: Received signal strength in dBm, or None if unknown.

    Returns:
        A tuple (label_string, bar_string).  The bar uses full-block
        (U+2588) and light-shade (U+2591) characters.  When dbm is None
        both parts indicate "no signal".
    """
    if dbm is None:
        return "---     ", "[" + " " * _NUM_BARS + "]"

    clamped = max(_RSSI_MIN, min(_RSSI_MAX, dbm))
    bars = round((clamped - _RSSI_MIN) / _RSSI_RANGE * _NUM_BARS)
    bar_str = "[" + "\u2588" * bars + "\u2591" * (_NUM_BARS - bars) + "]"

    if dbm >= _SIGNAL_MUKEMEL:
        label = "Mukemmel"
    elif dbm >= _SIGNAL_COK_IYI:
        label = "Cok Iyi "
    elif dbm >= _SIGNAL_IYI:
        label = "Iyi     "
    elif dbm >= _SIGNAL_ORTA:
        label = "Orta    "
    elif dbm >= _SIGNAL_ZAYIF:
        label = "Zayif   "
    else:
        label = "CokZayif"

    return label, bar_str


__all__ = [
    "dbm_to_mw",
    "mw_to_dbm",
    "variance",
    "variance_mw",
    "peak_to_peak",
    "zero_crossing_rate",
    "skewness",
    "kurtosis",
    "dominant_freq",
    "freq_label",
    "motion_magnitude",
    "sensitivity_params",
    "estimate_sampling_rate",
    "signal_quality",
]
