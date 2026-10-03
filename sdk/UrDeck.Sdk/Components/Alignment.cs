// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Components;

public enum HorizontalAlign
{
    Left,
    Center,
    Right,
}

public enum VerticalAlign
{
    Top,
    Middle,
    Bottom,
}

/// <summary>Which of the theme's two text colours a text line uses.</summary>
public enum TextEmphasis
{
    Primary,
    Muted,
}

/// <summary>Where a readout's unit sits relative to its value.</summary>
public enum UnitPlacement
{
    /// <summary>Aligned to the top of the value (for example the degree sign in 41°).</summary>
    Raised,
    /// <summary>On the value's baseline (for example PM after a time).</summary>
    Baseline,
}
