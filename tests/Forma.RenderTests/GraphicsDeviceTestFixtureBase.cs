// MonoGame - Copyright (C) MonoGame Foundation, Inc
// SPDX-License-Identifier: MS-PL
// Reduced adaptation of MonoGame's GraphicsDeviceTestFixtureBase;
// see THIRD-PARTY-NOTICES.md.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.RenderTests;

internal abstract class GraphicsDeviceTestFixtureBase
{
    protected Game game;
    protected GraphicsDeviceManager graphicsDeviceManager;
    protected GraphicsDevice gd;
    protected ContentManager content;

    [SetUp]
    public void SetUp()
    {
        game = new Game();
        graphicsDeviceManager = new GraphicsDeviceManager(game)
        {
            GraphicsProfile = GraphicsProfile.HiDef,
        };
        ((IGraphicsDeviceManager)game.Services.GetService(typeof(IGraphicsDeviceManager))).CreateDevice();
        gd = game.GraphicsDevice;
        content = game.Content;
        content.RootDirectory = "Content";
    }

    [TearDown]
    public void TearDown()
    {
        graphicsDeviceManager?.Dispose();
        game?.Dispose();
        game = null;
        graphicsDeviceManager = null;
        gd = null;
        content = null;
    }
}