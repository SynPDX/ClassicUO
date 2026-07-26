// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Assets;
using ClassicUO.Network;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using ClassicUO.Utility.Platforms;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class TopBarGump : Gump
    {
        private const string DiscordUrl = "https://discord.newbradford.com";
        private const string DonateUrl = "https://donate.newbradford.com";

        /// <summary>Idle caption hue (white).</summary>
        private const ushort MenuTextHue = 0x0481;
        /// <summary>Hover caption hue.</summary>
        private const ushort MenuTextHoverHue = 20095;

        /// <summary>Which category dropdown is currently open, if any.</summary>
        private Buttons? _openDropdown;

        private TopBarGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = false;

            // little
            Add(new ResizePic(0x13BE) { Width = 30, Height = 27 }, 2);

            Add(
                new Button(0, 0x15A1, 0x15A1, 0x15A1)
                {
                    X = 5,
                    Y = 3,
                    ToPage = 1
                },
                2
            );

            // big
            int smallWidth = 50;
            ref readonly var gumpInfo = ref Client.Game.UO.Gumps.GetGump(0x098B);
            if (gumpInfo.Texture != null)
            {
                smallWidth = gumpInfo.UV.Width;
            }

            int largeWidth = 100;

            gumpInfo = ref Client.Game.UO.Gumps.GetGump(0x098D);
            if (gumpInfo.Texture != null)
            {
                largeWidth = gumpInfo.UV.Width;
            }

            var cliloc = Client.Game.UO.FileManager.Clilocs;

            // widthType: 0 = small button, 1 = large button
            // Map + Stats are category menus; Debug/Connection/World Map live in dropdowns.
            (int widthType, Buttons button, string text)[] items =
            {
                (0, Buttons.Help, cliloc.GetString(3000134, ResGumps.Help)),
                (0, Buttons.Map, cliloc.GetString(3000430, ResGumps.Map)),
                (1, Buttons.Paperdoll, cliloc.GetString(3000133, ResGumps.Paperdoll)),
                (1, Buttons.Inventory, cliloc.GetString(3000431, ResGumps.Inventory)),
                (1, Buttons.Journal, cliloc.GetString(3000129, ResGumps.Journal)),
                (0, Buttons.Chat, cliloc.GetString(3000131, ResGumps.Chat)),
                (1, Buttons.Discord, "Discord"),
                (0, Buttons.Stats, "Stats"),
                (1, Buttons.Donate, "Donate"),
                (1, Buttons.UOStore, cliloc.GetString(1158008, ResGumps.UOStore)),
                (1, Buttons.GlobalChat, cliloc.GetString(1158390, ResGumps.GlobalChat))
            };

            bool hasUOStore = Client.Game.UO.Version >= ClientVersion.CV_706400;

            ResizePic background;

            Add(background = new ResizePic(0x13BE) { Height = 27 }, 1);

            Add(
                new Button(0, 0x15A4, 0x15A4, 0x15A4)
                {
                    X = 5,
                    Y = 3,
                    ToPage = 2
                },
                1
            );

            int startX = 30;

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];

                // Pre-UOStore clients: hide store + global chat (legacy ClassicUO behavior).
                if (!hasUOStore && (item.button == Buttons.UOStore || item.button == Buttons.GlobalChat))
                {
                    continue;
                }

                ushort graphic = (ushort)(item.widthType != 0 ? 0x098D : 0x098B);

                // White idle text; hue 20095 on hover.
                Add(
                    new RighClickableButton(
                        (int)item.button,
                        graphic,
                        graphic,
                        graphic,
                        item.text,
                        1,
                        true,
                        MenuTextHue,
                        MenuTextHoverHue
                    )
                    {
                        ButtonAction = ButtonAction.Activate,
                        X = startX,
                        Y = 1,
                        FontCenter = true
                    },
                    1
                );

                startX += (item.widthType != 0 ? largeWidth : smallWidth) + 1;
                background.Width = startX;
            }

            background.Width = startX + 1;

            //layer
            LayerOrder = UILayer.Over;
        }

        public bool IsMinimized { get; private set; }

        public static void Create(World world)
        {
            TopBarGump gump = UIManager.GetGump<TopBarGump>();

            if (gump == null)
            {
                if (
                    ProfileManager.CurrentProfile.TopbarGumpPosition.X < 0
                    || ProfileManager.CurrentProfile.TopbarGumpPosition.Y < 0
                )
                {
                    ProfileManager.CurrentProfile.TopbarGumpPosition = Point.Zero;
                }

                UIManager.Add(
                    gump = new TopBarGump(world)
                    {
                        X = ProfileManager.CurrentProfile.TopbarGumpPosition.X,
                        Y = ProfileManager.CurrentProfile.TopbarGumpPosition.Y
                    }
                );

                if (ProfileManager.CurrentProfile.TopbarGumpIsMinimized)
                {
                    gump.ChangePage(2);
                }
            }
            else
            {
                Log.Error(ResGumps.TopBarGumpAlreadyExists);
            }
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Right && (X != 0 || Y != 0))
            {
                X = 0;
                Y = 0;

                ProfileManager.CurrentProfile.TopbarGumpPosition = Location;
            }
        }

        public override void OnPageChanged()
        {
            ProfileManager.CurrentProfile.TopbarGumpIsMinimized = IsMinimized = ActivePage == 2;
            WantUpdateSize = true;
        }

        protected override void OnDragEnd(int x, int y)
        {
            base.OnDragEnd(x, y);
            ProfileManager.CurrentProfile.TopbarGumpPosition = Location;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch ((Buttons)buttonID)
            {
                case Buttons.Map:
                    ShowCategoryDropdown(
                        Buttons.Map,
                        ("Mini Map", () => GameActions.OpenMiniMap(World)),
                        ("World Map", () => GameActions.OpenWorldMap(World))
                    );

                    break;

                case Buttons.Stats:
                    ShowCategoryDropdown(
                        Buttons.Stats,
                        ("Debug", ToggleDebugGump),
                        ("Connection", ToggleConnectionGump)
                    );

                    break;

                case Buttons.Paperdoll:
                    GameActions.OpenPaperdoll(World, World.Player);

                    break;

                case Buttons.Inventory:
                    GameActions.OpenBackpack(World);

                    break;

                case Buttons.Journal:
                    GameActions.OpenJournal(World);

                    break;

                case Buttons.Chat:
                    GameActions.OpenChat(World);

                    break;

                case Buttons.GlobalChat:
                    // Same as Chat: server-side New Bradford global chat (0xB5).
                    GameActions.OpenChat(World);

                    break;

                case Buttons.UOStore:
                    if (Client.Game.UO.Version >= ClientVersion.CV_706400)
                    {
                        NetClient.Socket.Send_OpenUOStore();
                    }

                    break;

                case Buttons.Help:
                    GameActions.RequestHelp();

                    break;

                case Buttons.Discord:
                    PlatformHelper.LaunchBrowser(DiscordUrl);

                    break;

                case Buttons.Donate:
                    PlatformHelper.LaunchBrowser(DonateUrl);

                    break;
            }
        }

        private void ToggleDebugGump()
        {
            DebugGump debugGump = UIManager.GetGump<DebugGump>();

            if (debugGump == null)
            {
                debugGump = new DebugGump(World, 100, 100);
                UIManager.Add(debugGump);
            }
            else
            {
                debugGump.IsVisible = !debugGump.IsVisible;
                debugGump.SetInScreen();
            }
        }

        private void ToggleConnectionGump()
        {
            NetworkStatsGump netstatsgump = UIManager.GetGump<NetworkStatsGump>();

            if (netstatsgump == null)
            {
                netstatsgump = new NetworkStatsGump(World, 100, 100);
                UIManager.Add(netstatsgump);
            }
            else
            {
                netstatsgump.IsVisible = !netstatsgump.IsVisible;
                netstatsgump.SetInScreen();
            }
        }

        private void ShowCategoryDropdown(Buttons anchor, params (string text, Action action)[] items)
        {
            // Same category open → toggle closed.
            if (
                UIManager.ContextMenu != null
                && !UIManager.ContextMenu.IsDisposed
                && _openDropdown == anchor
            )
            {
                UIManager.ShowContextMenu(null);
                _openDropdown = null;

                return;
            }

            // Different category (or none) → replace with this dropdown.
            var menu = new ContextMenuControl(this);

            for (int i = 0; i < items.Length; i++)
            {
                var entry = items[i];
                menu.Add(entry.text, entry.action);
            }

            menu.Show();
            _openDropdown = anchor;
            PositionDropdownUnderButton(anchor);
        }

        /// <summary>
        /// Place the context menu flush under the category button (not at the mouse).
        /// </summary>
        private void PositionDropdownUnderButton(Buttons buttonId)
        {
            ContextMenuShowMenu cm = UIManager.ContextMenu;

            if (cm == null || cm.IsDisposed)
            {
                return;
            }

            Button anchorBtn = null;

            foreach (Button b in FindControls<Button>())
            {
                if (b.ButtonID == (int)buttonId)
                {
                    anchorBtn = b;

                    break;
                }
            }

            if (anchorBtn == null)
            {
                return;
            }

            int x = anchorBtn.ScreenCoordinateX;
            int y = anchorBtn.ScreenCoordinateY + anchorBtn.Height;

            // Keep on-screen (same idea as ContextMenuShowMenu).
            if (x + cm.Width > Client.Game.ClientBounds.Width)
            {
                x = Client.Game.ClientBounds.Width - cm.Width;
            }

            if (y + cm.Height > Client.Game.ClientBounds.Height)
            {
                y = anchorBtn.ScreenCoordinateY - cm.Height;

                if (y < 0)
                {
                    y = 0;
                }
            }

            if (x < 0)
            {
                x = 0;
            }

            cm.X = x;
            cm.Y = y;
        }

        private enum Buttons
        {
            // Start at 1 so we never collide with page-toggle buttons (ButtonID 0).
            Help = 1,
            Map = 2,
            Paperdoll = 3,
            Inventory = 4,
            Journal = 5,
            Chat = 6,
            Discord = 7,
            Stats = 8,
            Donate = 9,
            UOStore = 10,
            GlobalChat = 11
        }

        private class RighClickableButton : Button
        {
            public RighClickableButton(
                int buttonID,
                ushort normal,
                ushort pressed,
                ushort over = 0,
                string caption = "",
                byte font = 0,
                bool isunicode = true,
                ushort normalHue = ushort.MaxValue,
                ushort hoverHue = ushort.MaxValue
            ) : base(buttonID, normal, pressed, over, caption, font, isunicode, normalHue, hoverHue)
            { }

            public RighClickableButton(List<string> parts) : base(parts) { }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                base.OnMouseUp(x, y, button);
                Parent?.InvokeMouseUp(new Point(x, y), button);
            }
        }
    }
}
