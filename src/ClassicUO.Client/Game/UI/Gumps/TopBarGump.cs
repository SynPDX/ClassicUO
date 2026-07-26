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
        /// <summary>Hover caption hue (all menu buttons).</summary>
        private const ushort MenuTextHoverHue = 0x527;
        /// <summary>Donate button idle caption hue (0x4E9).</summary>
        private const ushort DonateTextHue = 0x4E9;
        /// <summary>Extra gap between category button and its dropdown.</summary>
        private const int DropdownOffsetY = 5;
        /// <summary>Frames to wait for a server gump after Help/Chat request.</summary>
        private const int ServerGumpCaptureFrames = 60;

        /// <summary>Which category dropdown is currently open, if any.</summary>
        private Buttons? _openDropdown;

        /// <summary>Server gump opened by Help (toggle-close on second click).</summary>
        private uint _helpGumpSerial;
        /// <summary>Server gump opened by Chat (toggle-close on second click).</summary>
        private uint _chatGumpSerial;

        private enum ServerGumpAwait
        {
            None,
            Help,
            Chat
        }

        private ServerGumpAwait _awaitServerGump;
        private HashSet<uint> _serverGumpsBefore;
        private int _awaitServerGumpFrames;

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
            // textHue: 0 = default MenuTextHue
            // Map + Stats are category menus; Debug/Connection/World Map live in dropdowns.
            (int widthType, Buttons button, string text, ushort textHue)[] items =
            {
                (0, Buttons.Help, cliloc.GetString(3000134, ResGumps.Help), 0),
                (0, Buttons.Map, cliloc.GetString(3000430, ResGumps.Map), 0),
                (1, Buttons.Paperdoll, cliloc.GetString(3000133, ResGumps.Paperdoll), 0),
                (1, Buttons.Inventory, cliloc.GetString(3000431, ResGumps.Inventory), 0),
                (1, Buttons.Journal, cliloc.GetString(3000129, ResGumps.Journal), 0),
                (0, Buttons.Chat, cliloc.GetString(3000131, ResGumps.Chat), 0),
                (1, Buttons.Discord, "Discord", 0),
                (0, Buttons.Stats, "Stats", 0),
                (1, Buttons.Donate, "Donate", DonateTextHue),
                (1, Buttons.UOStore, cliloc.GetString(1158008, ResGumps.UOStore), 0),
                (1, Buttons.GlobalChat, cliloc.GetString(1158390, ResGumps.GlobalChat), 0)
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
                ushort idleHue = item.textHue != 0 ? item.textHue : MenuTextHue;

                // Default: white idle, 0x527 hover. Donate uses DonateTextHue idle.
                Add(
                    new RighClickableButton(
                        (int)item.button,
                        graphic,
                        graphic,
                        graphic,
                        item.text,
                        1,
                        true,
                        idleHue,
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

        public override void Update()
        {
            base.Update();
            TryCaptureServerGump();
        }

        public override void OnButtonClick(int buttonID)
        {
            switch ((Buttons)buttonID)
            {
                case Buttons.Map:
                    ShowCategoryDropdown(
                        Buttons.Map,
                        ("Mini Map", ToggleMiniMap),
                        ("World Map", ToggleWorldMap)
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
                    TogglePaperdoll();

                    break;

                case Buttons.Inventory:
                    ToggleInventory();

                    break;

                case Buttons.Journal:
                    ToggleJournal();

                    break;

                case Buttons.Chat:
                case Buttons.GlobalChat:
                    // New Bradford: server-side global chat (packet 0xB5).
                    ToggleChat();

                    break;

                case Buttons.UOStore:
                    if (Client.Game.UO.Version >= ClientVersion.CV_706400)
                    {
                        NetClient.Socket.Send_OpenUOStore();
                    }

                    break;

                case Buttons.Help:
                    ToggleHelp();

                    break;

                case Buttons.Discord:
                    PlatformHelper.LaunchBrowser(DiscordUrl);

                    break;

                case Buttons.Donate:
                    PlatformHelper.LaunchBrowser(DonateUrl);

                    break;
            }
        }

        private void ToggleHelp()
        {
            if (TryCloseTrackedGump(ref _helpGumpSerial))
            {
                return;
            }

            BeginServerGumpCapture(ServerGumpAwait.Help);
            GameActions.RequestHelp();
        }

        private void ToggleChat()
        {
            if (TryCloseTrackedGump(ref _chatGumpSerial))
            {
                return;
            }

            BeginServerGumpCapture(ServerGumpAwait.Chat);
            GameActions.OpenChat(World);
        }

        private void TogglePaperdoll()
        {
            PaperDollGump paperDoll = UIManager.GetGump<PaperDollGump>(World.Player);

            if (paperDoll != null && !paperDoll.IsDisposed)
            {
                paperDoll.Dispose();

                return;
            }

            GameActions.OpenPaperdoll(World, World.Player);
        }

        private void ToggleInventory()
        {
            Item backpack = World.Player?.FindItemByLayer(Layer.Backpack);

            if (backpack != null)
            {
                ContainerGump backpackGump = UIManager.GetGump<ContainerGump>(backpack);

                if (backpackGump != null && !backpackGump.IsDisposed)
                {
                    backpackGump.Dispose();

                    return;
                }
            }

            GameActions.OpenBackpack(World);
        }

        private void ToggleJournal()
        {
            if (ProfileManager.CurrentProfile.UseAlternateJournal)
            {
                ResizableJournal alt = UIManager.GetGump<ResizableJournal>();

                if (alt != null && !alt.IsDisposed)
                {
                    alt.Dispose();

                    return;
                }

                UIManager.Add(new ResizableJournal(World));

                return;
            }

            JournalGump journal = UIManager.GetGump<JournalGump>();

            if (journal != null && !journal.IsDisposed)
            {
                journal.Dispose();

                return;
            }

            GameActions.OpenJournal(World);
        }

        private void ToggleMiniMap()
        {
            MiniMapGump miniMap = UIManager.GetGump<MiniMapGump>();

            if (miniMap != null && !miniMap.IsDisposed)
            {
                miniMap.Dispose();

                return;
            }

            UIManager.Add(new MiniMapGump(World));
        }

        private void ToggleWorldMap()
        {
            WorldMapGump worldMap = UIManager.GetGump<WorldMapGump>();

            if (worldMap != null && !worldMap.IsDisposed)
            {
                worldMap.Dispose();

                return;
            }

            GameActions.OpenWorldMap(World);
        }

        private void ToggleDebugGump()
        {
            DebugGump debugGump = UIManager.GetGump<DebugGump>();

            if (debugGump == null || debugGump.IsDisposed)
            {
                UIManager.Add(new DebugGump(World, 100, 100));
            }
            else if (debugGump.IsVisible)
            {
                debugGump.IsVisible = false;
            }
            else
            {
                debugGump.IsVisible = true;
                debugGump.SetInScreen();
                debugGump.BringOnTop();
            }
        }

        private void ToggleConnectionGump()
        {
            NetworkStatsGump netStats = UIManager.GetGump<NetworkStatsGump>();

            if (netStats == null || netStats.IsDisposed)
            {
                UIManager.Add(new NetworkStatsGump(World, 100, 100));
            }
            else if (netStats.IsVisible)
            {
                netStats.IsVisible = false;
            }
            else
            {
                netStats.IsVisible = true;
                netStats.SetInScreen();
                netStats.BringOnTop();
            }
        }

        private void BeginServerGumpCapture(ServerGumpAwait kind)
        {
            _serverGumpsBefore = new HashSet<uint>();

            foreach (Gump g in UIManager.Gumps)
            {
                if (!g.IsDisposed && g.ServerSerial != 0)
                {
                    _serverGumpsBefore.Add(g.LocalSerial);
                }
            }

            _awaitServerGump = kind;
            _awaitServerGumpFrames = ServerGumpCaptureFrames;
        }

        private void TryCaptureServerGump()
        {
            if (_awaitServerGump == ServerGumpAwait.None)
            {
                return;
            }

            if (--_awaitServerGumpFrames <= 0)
            {
                _awaitServerGump = ServerGumpAwait.None;
                _serverGumpsBefore = null;

                return;
            }

            foreach (Gump g in UIManager.Gumps)
            {
                if (
                    g.IsDisposed
                    || g.ServerSerial == 0
                    || (_serverGumpsBefore != null && _serverGumpsBefore.Contains(g.LocalSerial))
                )
                {
                    continue;
                }

                if (_awaitServerGump == ServerGumpAwait.Help)
                {
                    _helpGumpSerial = g.LocalSerial;
                }
                else if (_awaitServerGump == ServerGumpAwait.Chat)
                {
                    _chatGumpSerial = g.LocalSerial;
                }

                _awaitServerGump = ServerGumpAwait.None;
                _serverGumpsBefore = null;

                return;
            }
        }

        /// <summary>
        /// Close a previously opened server gump (Help/Chat). Uses button 0 when possible
        /// so the server is notified (same as right-click close).
        /// </summary>
        private static bool TryCloseTrackedGump(ref uint localSerial)
        {
            if (localSerial == 0)
            {
                return false;
            }

            Gump gump = UIManager.GetGump(localSerial);

            if (gump == null || gump.IsDisposed)
            {
                localSerial = 0;

                return false;
            }

            CloseGump(gump);
            localSerial = 0;

            return true;
        }

        private static void CloseGump(Gump gump)
        {
            if (gump == null || gump.IsDisposed)
            {
                return;
            }

            if (gump.ServerSerial != 0)
            {
                // Matches right-click close: reply button 0 then dispose.
                gump.OnButtonClick(0);
            }
            else
            {
                gump.Dispose();
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
            int y = anchorBtn.ScreenCoordinateY + anchorBtn.Height + DropdownOffsetY;

            // Keep on-screen (same idea as ContextMenuShowMenu).
            if (x + cm.Width > Client.Game.ClientBounds.Width)
            {
                x = Client.Game.ClientBounds.Width - cm.Width;
            }

            if (y + cm.Height > Client.Game.ClientBounds.Height)
            {
                y = anchorBtn.ScreenCoordinateY - cm.Height - DropdownOffsetY;

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
