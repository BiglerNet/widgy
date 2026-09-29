# UrDeck plugin exception

Additional permission under section 7 of the GNU General Public License, version 3 ("GPLv3"), granted by the
copyright holders of UrDeck for the UrDeck host, engine and official widgets (together, the "Program").

## Definitions

- **SDK Interface**: the public API a plugin uses to interact with the Program, meaning the public types, members and
  attributes of the `UrDeck.Sdk` and `UrDeck.Analyzer` assemblies and, until `UrDeck.Sdk` exists as a separate
  assembly, the public widget-authoring API of `UrDeck.Core` (`Widget<TConfig>`, `IWidget`, the widget attributes,
  `WidgetConfig`, the render context and the types they expose).
- **Plugin**: a separate module (a .NET assembly) that the Program loads at run time through its plugin loader and that
  interacts with the Program only through the SDK Interface.

## Permission

You may load a Plugin into the Program, and you may create, convey and license a Plugin under terms of your choice,
including proprietary terms. Running the Program together with a Plugin, or building a Plugin against the SDK
Interface, does not by itself make the Plugin subject to the GPLv3 or require you to license it under the GPLv3.

## Limits

- This permission does not apply to the Program itself or to any modified version of it, which remain under the
  GPLv3 and must be conveyed under it.
- It does not apply to code copied from the Program (including the official widgets) into a Plugin. Such code stays
  under the GPLv3, unless it is code that is separately licensed under the MIT License, such as the `UrDeck.Analyzer`
  assembly and the future `UrDeck.Sdk` assembly.

## Removal

If you modify the Program, you may extend this exception to your version, but you are not obliged to. If you do not
wish to, delete this exception notice from your version, as permitted by section 7 of the GPLv3.
