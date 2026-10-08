using System;
using System.Collections.Generic;
using System.IO;

using ClientCore;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A panel that displays a gallery of images with left/right navigation arrows.
    /// Images are scaled down (never up) to fit the panel while keeping their aspect
    /// ratio. The arrows are hidden when there is only one image or none. Clicking the
    /// image raises <see cref="ZoomRequested"/> so the owner can show a fullscreen view.
    /// </summary>
    public class DatabaseGallery : XNAPanel
    {
        private const int DEFAULT_ARROW_SIZE = 30;
        private const int DEFAULT_IMAGE_PADDING = 20;

        private readonly List<Texture2D> images = new();
        private int currentIndex;

        private XNAButton btnPrev;
        private XNAButton btnNext;

        public DatabaseGallery(WindowManager windowManager) : base(windowManager)
        {
            DrawMode = ControlDrawMode.UNIQUE_RENDER_TARGET;
            InputEnabled = true;
        }

        /// <summary>
        /// The size of the prev/next arrow buttons (both width and height).
        /// Editable via the <c>ArrowSize</c> INI attribute.
        /// </summary>
        public int ArrowSize { get; set; } = DEFAULT_ARROW_SIZE;

        /// <summary>
        /// The empty margin kept around the image inside the panel.
        /// Editable via the <c>ImagePadding</c> INI attribute.
        /// </summary>
        public int ImagePadding { get; set; } = DEFAULT_IMAGE_PADDING;

        /// <summary>
        /// Raised when the user clicks the gallery image to view it fullscreen.
        /// </summary>
        public event EventHandler ZoomRequested;

        /// <summary>
        /// Whether the gallery currently has any images.
        /// </summary>
        public bool HasImages => images.Count > 0;

        /// <summary>
        /// The current image's texture, or null if there are no images.
        /// </summary>
        public Texture2D CurrentImage => images.Count == 0 ? null : images[currentIndex];

        public override void Initialize()
        {
            base.Initialize();

            btnPrev = new XNAButton(WindowManager)
            {
                Name = nameof(btnPrev),
                Text = "<"
            };
            btnPrev.ClientRectangle = new Rectangle(4, Height / 2 - ArrowSize / 2, ArrowSize, ArrowSize);
            btnPrev.LeftClick += (s, e) => Previous();
            AddChild(btnPrev);

            btnNext = new XNAButton(WindowManager)
            {
                Name = nameof(btnNext),
                Text = ">"
            };
            btnNext.ClientRectangle = new Rectangle(Width - ArrowSize - 4, Height / 2 - ArrowSize / 2, ArrowSize, ArrowSize);
            btnNext.LeftClick += (s, e) => Next();
            AddChild(btnNext);

            UpdateArrowVisibility();
        }

        protected override void ParseControlINIAttribute(IniFile iniFile, string key, string value)
        {
            switch (key)
            {
                case "ArrowSize":
                    ArrowSize = Conversions.IntFromString(value, ArrowSize);
                    if (btnPrev != null)
                        PositionArrows();
                    return;
                case "ImagePadding":
                    ImagePadding = Conversions.IntFromString(value, ImagePadding);
                    return;
            }

            base.ParseControlINIAttribute(iniFile, key, value);
        }

        private void PositionArrows()
        {
            btnPrev.ClientRectangle = new Rectangle(4, Height / 2 - ArrowSize / 2, ArrowSize, ArrowSize);
            btnNext.ClientRectangle = new Rectangle(Width - ArrowSize - 4, Height / 2 - ArrowSize / 2, ArrowSize, ArrowSize);
        }

        /// <summary>
        /// Clears the gallery and loads images from the given file names (relative to
        /// the resources folder). Missing files are skipped.
        /// </summary>
        public void SetImages(IEnumerable<string> fileNames)
        {
            foreach (Texture2D texture in images)
                texture.Dispose();
            images.Clear();

            if (fileNames != null)
            {
                foreach (string name in fileNames)
                {
                    if (string.IsNullOrEmpty(name))
                        continue;

                    string path = SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), name);
                    if (!File.Exists(path))
                        continue;

                    Texture2D texture = AssetLoader.LoadTextureUncached(path);
                    if (texture != null)
                        images.Add(texture);
                }
            }

            currentIndex = 0;
            UpdateArrowVisibility();
        }

        public void Next()
        {
            if (images.Count <= 1)
                return;
            currentIndex = (currentIndex + 1) % images.Count;
        }

        public void Previous()
        {
            if (images.Count <= 1)
                return;
            currentIndex = (currentIndex - 1 + images.Count) % images.Count;
        }

        public override void OnLeftClick(InputEventArgs inputEventArgs)
        {
            base.OnLeftClick(inputEventArgs);

            if (images.Count > 0)
            {
                ZoomRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            DrawPanel();

            if (images.Count == 0)
            {
                if (DrawBorders)
                    DrawPanelBorders();
                DrawChildren(gameTime);
                return;
            }

            Texture2D image = images[currentIndex];

            int availableWidth = Width - ImagePadding * 2;
            int availableHeight = Height - ImagePadding * 2;
            float scale = Math.Min(1.0f,
                Math.Min((float)availableWidth / image.Width,
                         (float)availableHeight / image.Height));
            int drawWidth = (int)(image.Width * scale);
            int drawHeight = (int)(image.Height * scale);
            int drawX = (Width - drawWidth) / 2;
            int drawY = (Height - drawHeight) / 2;

            DrawTexture(image, new Rectangle(drawX, drawY, drawWidth, drawHeight), Color.White);

            if (DrawBorders)
                DrawPanelBorders();

            DrawChildren(gameTime);
        }

        private void UpdateArrowVisibility()
        {
            if (btnPrev == null || btnNext == null)
                return;

            bool showArrows = images.Count > 1;
            btnPrev.Visible = showArrows;
            btnNext.Visible = showArrows;
        }

        public override void Kill()
        {
            foreach (Texture2D texture in images)
                texture.Dispose();
            images.Clear();

            base.Kill();
        }
    }
}
