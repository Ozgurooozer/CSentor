from lib.stats import (
    variance,
    peak_to_peak,
    motion_magnitude,
    sensitivity_params,
    signal_quality,
    freq_label,
    zero_crossing_rate,
    skewness,
    kurtosis,
    estimate_sampling_rate,
    variance_mw,
    dominant_freq,
)


def test_variance_empty():
    assert variance([]) == 0.0


def test_variance_single():
    assert variance([5]) == 0.0


def test_variance_constant():
    assert variance([3, 3, 3, 3]) == 0.0


def test_variance_positive():
    result = variance([1, 2, 3, 4, 5])
    assert result == 2.5


def test_variance_population_ddof_zero():
    result = variance([1, 2, 3, 4, 5], ddof=0)
    assert result == 2.0


def test_variance_sample_ddof_default():
    result = variance([1, 2, 3, 4, 5])
    assert result == 2.5


def test_peak_to_peak_empty():
    assert peak_to_peak([]) == 0.0


def test_peak_to_peak_single():
    assert peak_to_peak([10]) == 0.0


def test_peak_to_peak_normal():
    assert peak_to_peak([1, 5, 3, 8, 2]) == 7.0


def test_motion_magnitude_very_small():
    assert motion_magnitude(1) == "Cok Kucuk"


def test_motion_magnitude_small():
    assert motion_magnitude(5) == "Kucuk"


def test_motion_magnitude_medium():
    assert motion_magnitude(20) == "Orta"


def test_motion_magnitude_large():
    assert motion_magnitude(50) == "Buyuk"


def test_motion_magnitude_very_large():
    assert motion_magnitude(150) == "Cok Buyuk"


def test_sensitivity_params_default():
    mult, min_v, min_d, min_p = sensitivity_params(15.0)
    assert mult > 0
    assert min_v > 0
    assert min_d > 0
    assert min_p > 0


def test_sensitivity_params_min():
    mult, min_v, min_d, min_p = sensitivity_params(0.1)
    assert mult > 0
    assert min_v > 0
    assert min_d > 0
    assert min_p > 0
    assert mult < 1.0


def test_signal_quality_none():
    label, bar = signal_quality(None)
    assert label == "---     "


def test_signal_quality_zero():
    label, bar = signal_quality(0)
    assert "---" not in label
    assert "[" in bar


def test_signal_quality_excellent():
    label, bar = signal_quality(-45)
    assert "Mukemmel" in label


def test_freq_label_zero():
    assert freq_label(0) == ""


def test_freq_label_rest():
    assert freq_label(0.1) == "Sakin"


def test_freq_label_breath():
    assert freq_label(0.3) == "Nefes~"


def test_freq_label_walk():
    assert freq_label(2.0) == "Yurume~"


def test_freq_label_fast():
    assert freq_label(3.0) == "Hizli hrt"


def test_dominant_freq_insufficient_samples():
    from lib.stats import dominant_freq
    freq, power = dominant_freq([1, 2, 3], sample_hz=1.9)
    assert freq == 0.0
    assert power == 0.0


def test_zero_crossing_rate_empty():
    assert zero_crossing_rate([]) == 0.0


def test_zero_crossing_rate_single():
    assert zero_crossing_rate([5]) == 0.0


def test_zero_crossing_rate_constant():
    assert zero_crossing_rate([3, 3, 3, 3]) == 0.0


def test_zero_crossing_rate_alternating():
    assert zero_crossing_rate([1, 2, 1, 2, 1]) == 1.0


def test_zero_crossing_rate_quiet():
    result = zero_crossing_rate([5, 5, 5, 5, 6, 6, 6, 6])
    assert abs(result - 0.142857) < 1e-5


def test_skewness_empty():
    assert skewness([]) == 0.0


def test_skewness_single():
    assert skewness([5]) == 0.0


def test_skewness_symmetric():
    result = skewness([1, 2, 3, 4, 5])
    assert abs(result) < 0.01


def test_skewness_right():
    result = skewness([1, 1, 2, 3, 10])
    assert result > 0


def test_skewness_left():
    result = skewness([1, 8, 9, 9, 10])
    assert result < 0


def test_kurtosis_empty():
    assert kurtosis([]) == 0.0


def test_kurtosis_single():
    assert kurtosis([5]) == 0.0


def test_kurtosis_normal():
    result = kurtosis([1, 2, 3, 4, 5])
    assert abs(result - (-1.64)) < 0.01


def test_kurtosis_uniform():
    result = kurtosis([1, 1, 2, 2, 3, 3, 4, 4])
    assert abs(result - (-1.565)) < 0.01


def test_estimate_sampling_rate_empty():
    assert estimate_sampling_rate([]) == 1.0


def test_estimate_sampling_rate_single():
    assert estimate_sampling_rate([5]) == 1.0


def test_estimate_sampling_rate_regular():
    result = estimate_sampling_rate([0, 0.5, 1.0, 1.5, 2.0])
    assert abs(result - 2.0) < 1e-10


def test_estimate_sampling_rate_irregular():
    result = estimate_sampling_rate([0, 0.3, 0.9, 1.2, 2.0])
    assert abs(result - (1.0 / 0.6)) < 1e-10


def test_estimate_sampling_rate_negative_or_zero():
    result = estimate_sampling_rate([0, 0, 0.5, 1.0])
    assert result >= 0


def test_variance_mw_empty():
    assert variance_mw([]) == 0.0


def test_variance_mw_single():
    assert variance_mw([5]) == 0.0


def test_variance_mw_equal():
    assert variance_mw([-50, -50, -50]) == 0.0


def test_variance_mw_specific():
    result = variance_mw([-50, -53])
    assert abs(result - 1.24407e-11) < 1e-15


def test_variance_mw_ordering():
    v1 = variance_mw([-50, -53])
    v2 = variance_mw([-50, -60])
    assert v2 > v1


def test_variance_linear_power():
    result_linear = variance([-50, -53], linear_power=True)
    result_normal = variance([-50, -53], linear_power=False)
    assert abs(result_linear - 1.24407e-11) < 1e-15
    assert abs(result_normal - 4.5) < 1e-10


def test_variance_linear_power_false_default():
    assert variance([-50, -53], linear_power=False) == variance([-50, -53])


def test_dominant_freq_too_few():
    freq, power = dominant_freq([1] * 15, sample_hz=1.9)
    assert freq == 0.0
    assert power == 0.0


def test_dominant_freq_no_sample_hz():
    try:
        dominant_freq([1] * 20, sample_hz=None)
        assert False
    except TypeError:
        pass


def test_dominant_freq_constant():
    freq, power = dominant_freq([5] * 20, sample_hz=1.9)
    assert abs(freq) < 1e-10
    assert abs(power) < 1e-10


def test_dominant_freq_returns_tuple():
    result = dominant_freq([5] * 16, sample_hz=1.9)
    assert isinstance(result, tuple)
    assert len(result) == 2


def test_freq_label_sakin():
    assert freq_label(0) == ""


def test_freq_label_nefes():
    assert freq_label(0.08) == "Sakin"


def test_freq_label_kucuk():
    assert freq_label(0.3) == "Nefes~"


def test_freq_label_yurume():
    assert freq_label(0.8) == "Kucuk hrt"


def test_freq_label_hizli():
    assert freq_label(3.0) == "Hizli hrt"


def test_signal_quality_zayif():
    label, bar = signal_quality(-85)
    assert "Zayif" in label


def test_signal_quality_orta():
    label, bar = signal_quality(-80)
    assert "Orta" in label


def test_signal_quality_mukemmel():
    label, bar = signal_quality(0)
    assert "Mukemmel" in label


def test_signal_quality_none_label():
    label, bar = signal_quality(None)
    assert label == "---     "


def test_import_all():
    from lib.stats import (
        zero_crossing_rate,
        skewness,
        kurtosis,
        estimate_sampling_rate,
        variance_mw,
        dominant_freq,
        freq_label,
        signal_quality,
        sensitivity_params,
        motion_magnitude,
        peak_to_peak,
        variance,
    )
