using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using ClientCore;
using ClientCore.Extensions;
using ClientGUI;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A full-screen article database window with a collapsible table-of-contents tree
    /// on the left and an article viewer (title, image gallery, mixed text/image body)
    /// on the right. Content is configured through an INI file, and the whole layout
    /// (window, tree panel, title, gallery, body, close button) is editable through
    /// the window's INI file (<c>DatabaseWindow.ini</c>), just like the campaign selector.
    /// </summary>
    public class DatabaseWindow : XNAWindow
    {
        // Default layout values. All of them can be overridden in DatabaseWindow.ini:
        //  - [DatabaseWindow] Size / BackgroundTexture / BorderColor / ContentIni
        //  - [TreePanel]      tree region position, node metrics, colors, fonts
        //  - [ContentPanel]   article viewer origin, gallery height and gaps
        //  - [lblTitle] / [gallery] / [richTextBlock] / [btnClose] / [pnlZoom]
        //    standard control sections (Location, Size, Text, FontIndex, colors, textures)
        private const int DEFAULT_TREE_X = 30;
        private const int DEFAULT_TREE_Y = 90;
        private const int DEFAULT_TREE_WIDTH = 320;
        private const int DEFAULT_TREE_BOTTOM_MARGIN = 50;
        private const int DEFAULT_NODE_HEIGHT = 24;
        private const int DEFAULT_TREE_ROW_SPACING = 2;
        private const int DEFAULT_SCROLL_STEP = 20;
        private const int DEFAULT_INDENT_PER_DEPTH = 18;

        private const int DEFAULT_CONTENT_X = 380;
        private const int DEFAULT_CONTENT_Y = 60;
        private const int DEFAULT_CONTENT_WIDTH = 860;
        private const int DEFAULT_GALLERY_HEIGHT = 260;
        private const int DEFAULT_TITLE_TO_GALLERY_GAP = 44;
        private const int DEFAULT_GALLERY_TO_TEXT_GAP = 12;
        private const int DEFAULT_CONTENT_BOTTOM_MARGIN = 60;

        private const string CONTENT_INI_DEFAULT = "Database.ini";
        private const string TREE_PANEL_SECTION = "TreePanel";
        private const string CONTENT_PANEL_SECTION = "ContentPanel";

        private readonly List<DatabaseTreeNode> allNodes = new();
        private readonly List<DatabaseTreeNode> visibleNodes = new();
        private DatabaseTreeNode selectedArticleNode;

        private XNALabel lblTitle;
        private DatabaseRichTextBlock richTextBlock;
        private DatabaseGallery gallery;
        private XNAClientButton btnClose;
        private XNAPanel pnlZoom;
        private Texture2D zoomTexture;

        private float treeScrollOffset;

        // Tree region (defaults, editable via [TreePanel]).
        private int treeX = DEFAULT_TREE_X;
        private int treeY = DEFAULT_TREE_Y;
        private int treeWidth = DEFAULT_TREE_WIDTH;
        private int treeBottomMargin = DEFAULT_TREE_BOTTOM_MARGIN;
        private int nodeHeight = DEFAULT_NODE_HEIGHT;
        private int rowSpacing = DEFAULT_TREE_ROW_SPACING;
        private int scrollStep = DEFAULT_SCROLL_STEP;
        private int indentPerDepth = DEFAULT_INDENT_PER_DEPTH;
        private int nodeFontIndex = 1;
        private Color nodeIdleColor = new Color(40, 40, 40, 220);
        private Color nodeHoverColor = new Color(60, 60, 60, 240);
        private Color nodeSelectedColor = new Color(85, 85, 85, 240);
        private Color nodeTextColor = new Color(230, 230, 230, 255);
        private string expandedArrow = "v";
        private string collapsedArrow = ">";

        // Article viewer origin (defaults, editable via [ContentPanel]).
        private int contentX = DEFAULT_CONTENT_X;
        private int contentY = DEFAULT_CONTENT_Y;
        private int contentWidth = DEFAULT_CONTENT_WIDTH;
        private int galleryHeight = DEFAULT_GALLERY_HEIGHT;
        private int titleToGalleryGap = DEFAULT_TITLE_TO_GALLERY_GAP;
        private int galleryToTextGap = DEFAULT_GALLERY_TO_TEXT_GAP;
        private int contentBottomMargin = DEFAULT_CONTENT_BOTTOM_MARGIN;

        private string contentIniName = CONTENT_INI_DEFAULT;

        // The window-level default fonts (after [lblTitle] / [richTextBlock] INI
        // overrides), used as fallback for articles that don't set their own fonts.
        private int defaultTitleFontIndex = 2;
        private int defaultBodyFontIndex = 1;

        public DatabaseWindow(WindowManager windowManager, string windowTitle)
            : base(windowManager)
        {
            Name = windowTitle;
        }

        public override void Initialize()
        {
            // Default to a full-screen window. [DatabaseWindow] Size/BackgroundTexture
            // from the INI override these once base.Initialize() reads the file.
            ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX, WindowManager.RenderResolutionY);
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 255), Width, Height);
            BorderColor = UISettings.ActiveSettings.PanelBorderColor;

            CreateControls();

            // Reads DatabaseWindow.ini and applies the [DatabaseWindow] attributes to
            // this window and every [<ControlName>] section to the matching control
            // (recursively, including the gallery's prev/next arrow buttons).
            base.Initialize();

            LoadWindowSettings();

            // Remember the window-level default fonts so articles can override them
            // (via TitleFontIndex / BodyFontIndex) and restore them when not set.
            defaultTitleFontIndex = lblTitle.FontIndex;
            defaultBodyFontIndex = richTextBlock.FontIndex;

            LoadContent();

            // Bring the derived layout in line with the (possibly INI-adjusted) rects.
            UpdateContentLayout();

            CenterOnParent();
        }

        private void CreateControls()
        {
            lblTitle = new XNALabel(WindowManager)
            {
                Name = nameof(lblTitle),
                FontIndex = 2,
                ClientRectangle = new Rectangle(contentX, contentY, 0, 0),
                Text = string.Empty
            };

            gallery = new DatabaseGallery(WindowManager)
            {
                Name = nameof(gallery),
                ClientRectangle = new Rectangle(contentX, contentY + titleToGalleryGap, contentWidth, galleryHeight),
                DrawBorders = true,
                BorderColor = UISettings.ActiveSettings.PanelBorderColor,
                BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 120), 2, 2),
                PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED
            };
            gallery.ZoomRequested += Gallery_ZoomRequested;

            richTextBlock = new DatabaseRichTextBlock(WindowManager)
            {
                Name = nameof(richTextBlock),
                ClientRectangle = new Rectangle(contentX, contentY + titleToGalleryGap + galleryHeight + galleryToTextGap,
                    contentWidth, Math.Max(50, Height - (contentY + titleToGalleryGap + galleryHeight + galleryToTextGap) - contentBottomMargin)),
                DrawBorders = true,
                BorderColor = UISettings.ActiveSettings.PanelBorderColor,
                BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 110), 2, 2),
                PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED
            };

            btnClose = new XNAClientButton(WindowManager)
            {
                Name = nameof(btnClose),
                Text = "Back".L10N("Client:Main:ButtonBack")
            };
            btnClose.ClientRectangle = new Rectangle(contentX, Height - 45,
                UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            CampaignButtonDefaults.ApplyDefaultButtonTexture(btnClose);
            btnClose.LeftClick += (s, e) => Disable();

            pnlZoom = new XNAPanel(WindowManager)
            {
                Name = nameof(pnlZoom),
                ClientRectangle = new Rectangle(0, 0, Width, Height),
                BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 200), 2, 2),
                PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED,
                InputEnabled = true,
                UpdateOrder = int.MaxValue,
                DrawOrder = int.MaxValue
            };
            pnlZoom.LeftClick += (s, e) => CloseZoom();
            pnlZoom.Disable();

            AddChild(lblTitle);
            AddChild(gallery);
            AddChild(richTextBlock);
            AddChild(btnClose);
            AddChild(pnlZoom);
        }

        /// <summary>
        /// Reads the window-specific layout settings from the INI file that was used to
        /// theme this window (ThemeIni), mirroring how the campaign selector reads its
        /// [ChapterButtons] and [CampaignGroups] sections.
        /// </summary>
        private void LoadWindowSettings()
        {
            if (ThemeIni == null)
                return;

            if (ThemeIni.SectionExists(TREE_PANEL_SECTION))
            {
                treeX = GetIniInt(TREE_PANEL_SECTION, "X", treeX);
                treeY = GetIniInt(TREE_PANEL_SECTION, "Y", treeY);
                treeWidth = GetIniInt(TREE_PANEL_SECTION, "Width", treeWidth);
                treeBottomMargin = GetIniInt(TREE_PANEL_SECTION, "BottomMargin", treeBottomMargin);
                nodeHeight = GetIniInt(TREE_PANEL_SECTION, "NodeHeight", nodeHeight);
                rowSpacing = GetIniInt(TREE_PANEL_SECTION, "RowSpacing", rowSpacing);
                scrollStep = GetIniInt(TREE_PANEL_SECTION, "ScrollWheelStep", scrollStep);
                indentPerDepth = GetIniInt(TREE_PANEL_SECTION, "IndentPerDepth", indentPerDepth);
                nodeFontIndex = GetIniInt(TREE_PANEL_SECTION, "NodeFontIndex", nodeFontIndex);
                nodeIdleColor = GetIniColor(TREE_PANEL_SECTION, "IdleColor", nodeIdleColor);
                nodeHoverColor = GetIniColor(TREE_PANEL_SECTION, "HoverColor", nodeHoverColor);
                nodeSelectedColor = GetIniColor(TREE_PANEL_SECTION, "SelectedColor", nodeSelectedColor);
                nodeTextColor = GetIniColor(TREE_PANEL_SECTION, "TextColor", nodeTextColor);
                expandedArrow = ThemeIni.GetStringValue(TREE_PANEL_SECTION, "ExpandedArrow", expandedArrow);
                collapsedArrow = ThemeIni.GetStringValue(TREE_PANEL_SECTION, "CollapsedArrow", collapsedArrow);
            }

            if (ThemeIni.SectionExists(CONTENT_PANEL_SECTION))
            {
                contentX = GetIniInt(CONTENT_PANEL_SECTION, "X", contentX);
                contentY = GetIniInt(CONTENT_PANEL_SECTION, "Y", contentY);
                contentWidth = GetIniInt(CONTENT_PANEL_SECTION, "Width", contentWidth);
                galleryHeight = GetIniInt(CONTENT_PANEL_SECTION, "GalleryHeight", galleryHeight);
                titleToGalleryGap = GetIniInt(CONTENT_PANEL_SECTION, "TitleToGalleryGap", titleToGalleryGap);
                galleryToTextGap = GetIniInt(CONTENT_PANEL_SECTION, "GalleryToTextGap", galleryToTextGap);
                contentBottomMargin = GetIniInt(CONTENT_PANEL_SECTION, "BottomMargin", contentBottomMargin);
            }
        }

        private int GetIniInt(string section, string key, int defaultValue)
        {
            return ThemeIni != null ? ThemeIni.GetIntValue(section, key, defaultValue) : defaultValue;
        }

        private Color GetIniColor(string section, string key, Color defaultValue)
        {
            if (ThemeIni == null)
                return defaultValue;

            string value = ThemeIni.GetStringValue(section, key, string.Empty);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return AssetLoader.GetColorFromString(value);
        }

        /// <summary>
        /// Handles the window-level "ContentIni" attribute; all other attributes are
        /// delegated to the base control attribute parser.
        /// </summary>
        protected override void ParseControlINIAttribute(IniFile iniFile, string key, string value)
        {
            if (key == "ContentIni")
            {
                if (!string.IsNullOrWhiteSpace(value))
                    contentIniName = value;
                return;
            }

            base.ParseControlINIAttribute(iniFile, key, value);
        }

        private void LoadContent()
        {
            string contentIniPath = SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), contentIniName);

            if (!SafePath.GetFile(contentIniPath).Exists)
            {
                richTextBlock.ClearContent();
                richTextBlock.AddText(("Database.ini not found.".L10N("Client:Database:IniMissing"))
                    .Replace("Database.ini", contentIniName));
                return;
            }

            IniFile ini = new CCIniFile(contentIniPath);
            Dictionary<string, DatabaseTreeNode> nodesByKey = new();

            // The optional [Database] section can define a default window title.
            lblTitle.Text = ini.GetStringValue("Database", "Title", string.Empty).L10N("INI:Database:Title");

            // Parse all nodes first (any INI section other than [Database]).
            foreach (string sectionName in ini.GetSections())
            {
                if (sectionName.Equals("Database", StringComparison.OrdinalIgnoreCase))
                    continue;

                IniSection section = ini.GetSection(sectionName);
                string type = section.GetStringValue("Type", "Article");
                string text = section.GetStringValue("Text", sectionName).L10N($"INI:Database:{sectionName}:Text");

                var node = new DatabaseTreeNode(WindowManager)
                {
                    NodeText = text,
                    IsCategory = type.Equals("Category", StringComparison.OrdinalIgnoreCase),
                    Depth = 0,
                    IsExpanded = false,
                    Width = treeWidth,
                    RowHeight = nodeHeight,
                    IndentPerDepth = indentPerDepth,
                    FontIndex = nodeFontIndex,
                    IdleColor = nodeIdleColor,
                    HoverColor = nodeHoverColor,
                    SelectedColor = nodeSelectedColor,
                    TextColor = nodeTextColor,
                    ExpandedArrow = expandedArrow,
                    CollapsedArrow = collapsedArrow
                };
                node.Toggled += Node_Toggled;
                node.Selected += Node_Selected;

                // Store the raw section so article content can be loaded lazily.
                node.SectionName = sectionName;

                AddChild(node);
                nodesByKey[sectionName] = node;
                allNodes.Add(node);
            }

            // Build the tree via the Parent key (empty/missing = root level).
            foreach (DatabaseTreeNode node in nodesByKey.Values)
            {
                IniSection section = ini.GetSection(node.SectionName);
                string parentKey = section.GetStringValue("Parent", string.Empty);

                if (!string.IsNullOrEmpty(parentKey) && nodesByKey.TryGetValue(parentKey, out DatabaseTreeNode parent))
                {
                    node.Depth = parent.Depth + 1;
                    parent.Children.Add(node);
                }
            }

            // Keep a reference to the content section for lazy loading.
            foreach (DatabaseTreeNode node in allNodes)
                node.ContentSection = ini.GetSection(node.SectionName);

            RebuildVisibleNodes();
        }

        private void Node_Toggled(object sender, EventArgs e)
        {
            RebuildVisibleNodes();
        }

        private void Node_Selected(object sender, EventArgs e)
        {
            var node = (DatabaseTreeNode)sender;
            SelectArticle(node);
        }

        private void RebuildVisibleNodes()
        {
            visibleNodes.Clear();

            foreach (DatabaseTreeNode node in allNodes)
                node.Visible = false;

            foreach (DatabaseTreeNode node in allNodes)
            {
                if (node.Depth == 0)
                    AddVisibleRecursive(node);
            }

            RepositionNodes();
        }

        private void AddVisibleRecursive(DatabaseTreeNode node)
        {
            node.Visible = true;
            visibleNodes.Add(node);

            if (node.IsCategory && node.IsExpanded)
            {
                foreach (DatabaseTreeNode child in node.Children)
                    AddVisibleRecursive(child);
            }
        }

        private void RepositionNodes()
        {
            int y = treeY - (int)treeScrollOffset;

            foreach (DatabaseTreeNode node in visibleNodes)
            {
                node.ClientRectangle = new Rectangle(treeX, y, treeWidth, nodeHeight);
                y += nodeHeight + rowSpacing;
            }
        }

        private void SelectArticle(DatabaseTreeNode node)
        {
            if (node.IsCategory)
                return;

            if (selectedArticleNode != null)
                selectedArticleNode.SelectedFlag = false;
            selectedArticleNode = node;
            node.SelectedFlag = true;

            LoadArticle(node);
        }

        private void LoadArticle(DatabaseTreeNode node)
        {
            IniSection section = node.ContentSection;
            if (section == null)
            {
                lblTitle.Text = string.Empty;
                lblTitle.FontIndex = defaultTitleFontIndex;
                richTextBlock.FontIndex = defaultBodyFontIndex;
                richTextBlock.ClearContent();
                return;
            }

            lblTitle.Text = section.GetStringValue("Text", string.Empty).L10N($"INI:Database:{node.SectionName}:Text");

            // Per-article fonts: TitleFontIndex (article title) and BodyFontIndex
            // (default font of the body paragraphs).
            lblTitle.FontIndex = GetOptionalInt(section, "TitleFontIndex") ?? defaultTitleFontIndex;
            richTextBlock.FontIndex = GetOptionalInt(section, "BodyFontIndex") ?? defaultBodyFontIndex;

            // Gallery images (Image1..N).
            List<string> galleryNames = new();
            for (int i = 1; i <= 20; i++)
            {
                string imageName = section.GetStringValue("Image" + i, string.Empty);
                if (string.IsNullOrEmpty(imageName))
                    break;
                galleryNames.Add(imageName);
            }
            gallery.SetImages(galleryNames);

            // Body paragraphs: Description with @@ separating text paragraphs,
            // [FONT:n] markers switching the font and [IMG:filename] placeholders
            // inserting images at any position.
            richTextBlock.ClearContent();

            string description = section.GetStringValue("Description", string.Empty).L10N($"INI:Database:{node.SectionName}:Description");
            ParseBodyWithImages(description);

            UpdateContentLayout();
        }

        private static int? GetOptionalInt(IniSection section, string key)
        {
            string value = section.GetStringValue(key, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                return result;

            return null;
        }

        /// <summary>
        /// Parses the article body. "@@" separates paragraphs, [IMG:filename] inserts an
        /// image paragraph, [FONT:n] switches the font of the following text and
        /// [U]/[/U] / [S]/[/S] toggle underline/strikethrough. The style and font markers
        /// are INLINE: they never break the paragraph, so surrounding text keeps flowing
        /// on the same line.
        /// </summary>
        private void ParseBodyWithImages(string description)
        {
            if (string.IsNullOrEmpty(description))
                return;

            int currentFont = richTextBlock.FontIndex;
            DatabaseTextStyle currentStyle = DatabaseTextStyle.None;
            bool paragraphClosed = true;
            string pendingText = string.Empty;
            string remaining = description;

            // Flushes the accumulated text as a paragraph (or as an inline continuation
            // of the current paragraph after an inline marker).
            void FlushPendingText()
            {
                string trimmed = pendingText.Trim('\r', '\n');

                // Content inside an active style is always emitted (even when it is empty
                // or only whitespace) so the underline/strikethrough decoration stays
                // visible across the whole span.
                if (string.IsNullOrWhiteSpace(trimmed) && currentStyle == DatabaseTextStyle.None)
                {
                    pendingText = string.Empty;
                    return;
                }

                if (paragraphClosed)
                {
                    richTextBlock.AddText(trimmed, currentFont, currentStyle);
                    paragraphClosed = false;
                }
                else
                {
                    richTextBlock.AppendText(trimmed, currentFont, currentStyle);
                }

                pendingText = string.Empty;
            }

            while (true)
            {
                int markerStart = int.MaxValue;
                int markerKind = 0;

                void Consider(int index, int kind)
                {
                    if (index >= 0 && index < markerStart)
                    {
                        markerStart = index;
                        markerKind = kind;
                    }
                }

                Consider(remaining.IndexOf("@@", StringComparison.Ordinal), 1);
                Consider(remaining.IndexOf("[FONT:", StringComparison.OrdinalIgnoreCase), 2);
                Consider(remaining.IndexOf("[IMG:", StringComparison.OrdinalIgnoreCase), 3);
                Consider(remaining.IndexOf("[U]", StringComparison.OrdinalIgnoreCase), 4);
                Consider(remaining.IndexOf("[/U]", StringComparison.OrdinalIgnoreCase), 5);
                Consider(remaining.IndexOf("[S]", StringComparison.OrdinalIgnoreCase), 6);
                Consider(remaining.IndexOf("[/S]", StringComparison.OrdinalIgnoreCase), 7);

                if (markerKind == 0)
                {
                    pendingText += remaining;
                    FlushPendingText();
                    return;
                }

                pendingText += remaining.Substring(0, markerStart);

                switch (markerKind)
                {
                    case 1: // @@
                        FlushPendingText();
                        paragraphClosed = true;
                        remaining = remaining.Substring(markerStart + 2);
                        continue;
                    case 4: // [U]
                        FlushPendingText();
                        currentStyle |= DatabaseTextStyle.Underline;
                        remaining = remaining.Substring(markerStart + 3);
                        continue;
                    case 5: // [/U]
                        FlushPendingText();
                        currentStyle &= ~DatabaseTextStyle.Underline;
                        remaining = remaining.Substring(markerStart + 4);
                        continue;
                    case 6: // [S]
                        FlushPendingText();
                        currentStyle |= DatabaseTextStyle.Strikethrough;
                        remaining = remaining.Substring(markerStart + 3);
                        continue;
                    case 7: // [/S]
                        FlushPendingText();
                        currentStyle &= ~DatabaseTextStyle.Strikethrough;
                        remaining = remaining.Substring(markerStart + 4);
                        continue;
                }

                // [FONT:...] and [IMG:...] need their closing bracket.
                int markerEnd = remaining.IndexOf(']', markerStart);
                if (markerEnd < 0)
                {
                    // Unclosed marker; treat the rest as literal text.
                    pendingText += remaining.Substring(markerStart);
                    FlushPendingText();
                    return;
                }

                if (markerKind == 2)
                {
                    FlushPendingText();
                    string markerBody = remaining.Substring(markerStart + 6, markerEnd - markerStart - 6).Trim();
                    if (int.TryParse(markerBody, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fontIndex))
                        currentFont = fontIndex;
                }
                else
                {
                    FlushPendingText();
                    string markerBody = remaining.Substring(markerStart + 5, markerEnd - markerStart - 5).Trim();
                    Texture2D texture = LoadImage(markerBody);
                    if (texture != null)
                        richTextBlock.AddImage(texture);
                    paragraphClosed = true;
                }

                remaining = remaining.Substring(markerEnd + 1);
            }
        }

        /// <summary>
        /// Positions the gallery and the body based on whether the gallery has images.
        /// Controls that were explicitly placed via the window INI ([gallery] /
        /// [richTextBlock] sections) keep their INI positions; only the others are
        /// auto-arranged.
        /// </summary>
        private void UpdateContentLayout()
        {
            bool galleryPinned = IsLayoutPinned(nameof(gallery));
            bool textPinned = IsLayoutPinned(nameof(richTextBlock));

            // The article stack anchors to the title label's actual position, so moving
            // the title via [lblTitle] moves the gallery and body along with it.
            Rectangle titleRect = lblTitle.ClientRectangle;

            if (gallery.HasImages)
            {
                gallery.Visible = true;

                if (!galleryPinned)
                    gallery.ClientRectangle = new Rectangle(titleRect.X, titleRect.Y + titleToGalleryGap, contentWidth, galleryHeight);

                if (!textPinned)
                {
                    Rectangle g = gallery.ClientRectangle;
                    richTextBlock.ClientRectangle = new Rectangle(g.X, g.Y + g.Height + galleryToTextGap, g.Width,
                        Math.Max(50, Height - (g.Y + g.Height + galleryToTextGap) - contentBottomMargin));
                }
            }
            else
            {
                gallery.Visible = false;

                if (!textPinned)
                {
                    richTextBlock.ClientRectangle = new Rectangle(titleRect.X, titleRect.Y + titleToGalleryGap, contentWidth,
                        Math.Max(50, Height - (titleRect.Y + titleToGalleryGap) - contentBottomMargin));
                }
            }
        }

        /// <summary>
        /// Whether the given control's INI section explicitly defines a position or
        /// size. Only an explicit position/size disables the automatic article layout;
        /// sections that merely exist (e.g. all keys commented out) or only set styling
        /// keys are treated as unpinned so the layout can arrange itself.
        /// </summary>
        private bool IsLayoutPinned(string controlName)
        {
            if (ThemeIni == null)
                return false;

            IniSection section = ThemeIni.GetSection(controlName);
            if (section == null)
                return false;

            return section.KeyExists("Location") || section.KeyExists("Size")
                || section.KeyExists("X") || section.KeyExists("Y")
                || section.KeyExists("Width") || section.KeyExists("Height")
                || section.KeyExists("FillWidth") || section.KeyExists("FillHeight")
                || section.KeyExists("DistanceFromRightBorder") || section.KeyExists("DistanceFromBottomBorder");
        }

        private Texture2D LoadImage(string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return null;

            string path = SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), imageName);
            if (!File.Exists(path))
                return null;

            return AssetLoader.LoadTextureUncached(path);
        }

        private void Gallery_ZoomRequested(object sender, EventArgs e)
        {
            Texture2D image = gallery.CurrentImage;
            if (image == null)
                return;

            zoomTexture = image;
            pnlZoom.Enable();
        }

        private void CloseZoom()
        {
            zoomTexture = null;
            pnlZoom.Disable();
        }

        public override void OnMouseScrolled(InputEventArgs inputEventArgs)
        {
            base.OnMouseScrolled(inputEventArgs);

            Point cursor = GetCursorPoint();
            if (cursor.X >= treeX && cursor.X <= treeX + treeWidth &&
                cursor.Y >= treeY && cursor.Y <= Height - treeBottomMargin)
            {
                treeScrollOffset += Cursor.ScrollWheelValue > 0 ? -scrollStep : scrollStep;
                float maxOffset = Math.Max(0, visibleNodes.Count * (nodeHeight + rowSpacing) - (Height - treeBottomMargin - treeY));
                treeScrollOffset = Math.Clamp(treeScrollOffset, 0, maxOffset);
                RepositionNodes();
                inputEventArgs.Handled = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            // Draw the fullscreen zoomed image on top of everything when active.
            if (pnlZoom != null && pnlZoom.Enabled && zoomTexture != null)
            {
                int availableWidth = Width - 60;
                int availableHeight = Height - 60;
                float scale = Math.Min((float)availableWidth / zoomTexture.Width,
                                       (float)availableHeight / zoomTexture.Height);
                int drawWidth = (int)(zoomTexture.Width * scale);
                int drawHeight = (int)(zoomTexture.Height * scale);
                int drawX = (Width - drawWidth) / 2;
                int drawY = (Height - drawHeight) / 2;

                DrawTexture(zoomTexture,
                    new Rectangle(drawX, drawY, drawWidth, drawHeight), Color.White);
            }
        }

        public override void Kill()
        {
            zoomTexture = null;
            richTextBlock?.DisposeImages();

            base.Kill();
        }
    }
}
