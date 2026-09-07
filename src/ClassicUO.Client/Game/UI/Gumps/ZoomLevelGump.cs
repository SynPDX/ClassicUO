// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using System;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ZoomLevelGump : Gump
    {
        private const uint HOLD_AFTER_CTRL_MS = 500;
        private const uint FADE_MS = 300;
        private const float BACKGROUND_ALPHA = 0.7f;

        private static Point _last_position = new Point(int.MinValue, int.MinValue);

        private readonly AlphaBlendControl _background;
        private string _cacheText = string.Empty;
        private uint _ctrlReleasedAt;
        private float _fade = 1f;

        private ZoomLevelGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithEsc = false;
            CanCloseWithRightClick = false;
            AcceptMouseInput = true;
            AcceptKeyboardInput = false;
            LayerOrder = UILayer.Over;
            WantUpdateSize = false;

            Add
            (
                _background = new AlphaBlendControl(BACKGROUND_ALPHA)
                {
                    Width = Width,
                    Height = Height
                }
            );
        }

        public static void Show(World world)
        {
            ZoomLevelGump gump = UIManager.GetGump<ZoomLevelGump>();

            if (gump == null || gump.IsDisposed)
            {
                gump = new ZoomLevelGump(world);
                UIManager.Add(gump);
            }

            gump.NotifyZoomChanged();
        }

        public void NotifyZoomChanged()
        {
            _ctrlReleasedAt = 0;
            SetFade(1f);
            UpdateTextAndSize();
            ApplyPosition();
        }

        public override void Update()
        {
            base.Update();

            if (IsDisposed)
            {
                return;
            }

            if (!World.InGame)
            {
                Dispose();

                return;
            }

            UpdateTextAndSize();

            if (Keyboard.Ctrl)
            {
                _ctrlReleasedAt = 0;
                SetFade(1f);

                return;
            }

            if (_ctrlReleasedAt == 0)
            {
                _ctrlReleasedAt = Time.Ticks;
            }

            uint elapsed = Time.Ticks - _ctrlReleasedAt;

            if (elapsed < HOLD_AFTER_CTRL_MS)
            {
                SetFade(1f);

                return;
            }

            uint fadeElapsed = elapsed - HOLD_AFTER_CTRL_MS;

            if (fadeElapsed >= FADE_MS)
            {
                Dispose();

                return;
            }

            SetFade(1f - fadeElapsed / (float)FADE_MS);
        }

        protected override void OnMouseWheel(MouseEventType delta)
        {
            if (Keyboard.Ctrl && ProfileManager.CurrentProfile is { EnableMousewheelScaleZoom: true })
            {
                if (delta == MouseEventType.WheelScrollUp)
                {
                    Client.Game.Scene.Camera.ZoomIn();
                }
                else if (delta == MouseEventType.WheelScrollDown)
                {
                    Client.Game.Scene.Camera.ZoomOut();
                }

                NotifyZoomChanged();

                return;
            }

            base.OnMouseWheel(delta);
        }

        protected override void OnDragEnd(int x, int y)
        {
            base.OnDragEnd(x, y);
            SavePosition();
        }

        protected override void OnMove(int x, int y)
        {
            base.OnMove(x, y);
            SavePosition();
        }

        public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
        {
            if (!base.AddToRenderLists(renderLists, x, y, ref layerDepthRef))
            {
                return false;
            }

            float layerDepth = layerDepthRef;
            Vector3 hueVector = ShaderHueTranslator.GetHueVector(0, false, _fade);

            renderLists.AddGumpNoAtlas(
                batcher =>
                {
                    batcher.DrawString
                    (
                        Fonts.Bold,
                        _cacheText,
                        x + 12,
                        y + 10,
                        hueVector,
                        layerDepth
                    );

                    return true;
                }
            );

            return true;
        }

        private void SetFade(float fade)
        {
            _fade = fade;
            _background.Alpha = BACKGROUND_ALPHA * fade;
        }

        private void UpdateTextAndSize()
        {
            float zoom = Client.Game.Scene.Camera.Zoom;
            int percent = (int)MathF.Round(100f / zoom);
            string text = $"{percent}%";

            if (text == _cacheText && Width > 0)
            {
                return;
            }

            _cacheText = text;
            Vector2 size = Fonts.Bold.MeasureString(_cacheText);
            _background.Width = Width = (int)(size.X + 24);
            _background.Height = Height = (int)(size.Y + 20);
        }

        private void ApplyPosition()
        {
            if (_last_position.X != int.MinValue && _last_position.Y != int.MinValue)
            {
                X = _last_position.X;
                Y = _last_position.Y;
                SetInScreen();

                return;
            }

            Camera camera = Client.Game.Scene.Camera;
            X = camera.Bounds.X + ((camera.Bounds.Width - Width) >> 1);
            Y = camera.Bounds.Y + ((camera.Bounds.Height - Height) >> 1);
        }

        private void SavePosition()
        {
            _last_position.X = ScreenCoordinateX;
            _last_position.Y = ScreenCoordinateY;
        }
    }
}
