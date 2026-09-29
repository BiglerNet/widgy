// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CategoryAttribute : Attribute
{
    public string Name { get; }

    public CategoryAttribute(string name)
    {
        Name = name;
    }
}
