// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CategoryAttribute : Attribute
{
    public string Name { get; }

    public CategoryAttribute(string name)
    {
        Name = name;
    }
}
