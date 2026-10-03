// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk;

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
