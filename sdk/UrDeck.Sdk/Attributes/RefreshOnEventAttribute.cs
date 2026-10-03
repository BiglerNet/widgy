// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RefreshOnEventAttribute : Attribute
{
    public string EventName { get; }

    public RefreshOnEventAttribute(string eventName)
    {
        EventName = eventName;
    }
}
