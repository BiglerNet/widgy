// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RefreshAdaptiveAttribute : Attribute
{
    public double MinMs { get; }
    public double MaxMs { get; }

    public RefreshAdaptiveAttribute(double minMs, double maxMs)
    {
        MinMs = minMs;
        MaxMs = maxMs;
    }
}
