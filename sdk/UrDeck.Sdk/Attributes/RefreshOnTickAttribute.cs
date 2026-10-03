// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RefreshOnTickAttribute : Attribute
{
    public double Interval { get; }
    public TimeUnit Unit { get; }

    public RefreshOnTickAttribute(double interval, TimeUnit unit)
    {
        Interval = interval;
        Unit = unit;
    }
}
