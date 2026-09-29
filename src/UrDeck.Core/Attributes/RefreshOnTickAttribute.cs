// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Core.Enums;

namespace UrDeck.Core.Attributes;

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
