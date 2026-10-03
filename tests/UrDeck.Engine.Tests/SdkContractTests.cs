// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class SdkContractTests
{
    [Fact]
    public void SdkAssemblyVersion_IsFrozen()
    {
        // Plugins bind to the host's UrDeck.Sdk by assembly version; a bump makes already-built plugins load a second
        // SDK copy and silently fail. Only change this together with a deliberate, announced breaking SDK change.
        var version = typeof(Widget<>).Assembly.GetName().Version;
        Assert.Equal(new Version(0, 1, 0, 0), version);
    }
}
