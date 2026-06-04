using System;
using System.Collections.Generic;
using System.Globalization;
using WifiMotion.Core;

namespace WifiMotion.Testing;

/// <summary>
/// Test kaydini yonetir. Python <c>TestRecorder</c> sinifinin portu.
/// </summary>
public sealed class TestRecorder
{
    public bool Active { get; private set; }
    public TestRecording? Recording { get; private set; }

    public double Remaining
    {
        get
        {
            if (!Active || Recording is null)
                return 0.0;
            return Math.Max(0.0, Recording.Duration - (TestRecording.Now() - Recording.StartTime));
        }
    }

    public double Elapsed
    {
        get
        {
            if (!Active || Recording is null)
                return 0.0;
            return TestRecording.Now() - Recording.StartTime;
        }
    }

    public string TestName => Recording?.Name ?? "";

    public string CurrentInstruction =>
        Active && Recording is not null ? Recording.CurrentInstruction : "";

    public void Start(string name, int duration, IReadOnlyList<TestPhase>? phases = null)
    {
        Recording = new TestRecording
        {
            Name = name,
            Duration = duration,
            StartTime = TestRecording.Now(),
            Phases = phases ?? Array.Empty<TestPhase>(),
        };
        Active = true;
    }

    public void Record(int dbm, int rssiPct, double varVal, double deltaVal, double ptpVal,
        double rx, double tx, IReadOnlyList<ApSlot> apSlots)
    {
        if (!Active || Recording is null)
            return;
        Recording.AddSample(dbm, rssiPct, varVal, deltaVal, ptpVal, rx, tx);
        foreach (var s in apSlots)
        {
            if (!Recording.ApHistory.TryGetValue(s.Bssid, out var list))
            {
                list = new List<double>();
                Recording.ApHistory[s.Bssid] = list;
            }
            list.Add(s.Rssi);
        }
        Recording.AnnotateIfDue();
    }

    public void Annotate(string text)
    {
        if (Active && Recording is not null)
        {
            string actual = $"[{DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}] {text}";
            Recording.Annotate(actual);
        }
    }

    public TestRecording? Stop()
    {
        if (!Active || Recording is null)
            return null;
        var rec = Recording;
        Active = false;
        Recording = null;
        return rec;
    }
}
