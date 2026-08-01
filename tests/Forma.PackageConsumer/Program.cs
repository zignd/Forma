// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;
using Microsoft.Xna.Framework;

using var context = new UIContext();
var root = new VBoxContainer
{
    Size = new Vector2(320, 180),
};
root.AddChild(new Label { Text = "Forma package consumer" });
root.AddChild(new Button { Text = "Continue" });
context.Add(root);
context.Layout();
using var video = new VideoStreamPlayer();

return context.Roots.Count == 1 ? 0 : 1;