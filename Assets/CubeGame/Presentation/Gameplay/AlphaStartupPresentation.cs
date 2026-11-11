using UnityEngine;
using UnityEngine.UIElements;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Presentation facts of the Alpha 2013 start screens, recovered from the client binary and validated against
    /// captures of the running original. Every number here comes from the original code path, not from taste:
    ///
    ///   StartMenuWidget::vfunc_1 0x583320 — three items, text size 0x41900000 = 18.0f, y = 0x41F00000 = 30,
    ///     0x42A00000 = 80, 0x43020000 = 130 (pitch 50), hit zones 10..40 / 60..90 / 110..140, x = half of the
    ///     display width (the text is centred). Each item is drawn twice: once with an extra size argument
    ///     0x40800000 = 4.0f and a black colour (the outline pass) and once with the item colour
    ///     0x3E4CCCCD = 0.2f, 1.0f, 1.0f (hover) or 1,1,1 (normal), which reproduces the thick black outline
    ///     Alpha text has.
    ///   SystemWidget 0x587710 — the same three-item layout with Options / Start Menu / Exit Game.
    ///   GameController_buildCharacterList 0x49D650, GameController_buildWorldList 0x4A23D0 — rows of 0xE6 = 230 px
    ///     starting at y = 0x32 = 50, count + 1 rows (the last row creates a new entry).
    ///
    /// Alpha text uses no font scaling: the UI font (L"resource1.dat", loaded through
    /// FontCache_find_or_create_scalable by every widget) is a small-caps bitmap design, so any mixed-case string
    /// renders as capitals — label strings are kept exactly as the original string table has them.
    /// </summary>
    internal static class AlphaStartupPresentation
    {
        // ---- Font (ALPHA_CODE_STATIC: StartMenuWidget.cpp:109, SystemWidget.cpp:109, Interface/World/Chat/Speech) ----
        public const string FontResourceName = "AlphaFont";
        public const string FontSourceFile = "resource1.dat";
        public const string FontSecondSourceFile = "resource2.dat";

        // ---- Text (ALPHA_CODE_STATIC: start menu 18.0f; SystemWidget 14.0f and pitch 30; Options 12.0f and pitch 30;
        //      row text of 0x49D650 / 0x4A23D0 is 14 / 12 / 10) ----
        public const int TextSize = 18;
        public const int SystemTextSize = 14;
        public const int SystemItemPitch = 30;
        public const int OptionTextSize = 12;
        public const int OptionRowPitch = 30;
        public const int NameSize = 14;
        public const int TitleSize = 12;
        public const int DetailSize = 10;
        public const float OutlineSize = 4f;
        public const float HoverColor = 0.2f;

        // Row text origin inside a 600x220 row (ALPHA_CODE_STATIC: text calls at 200,30 / 200,55 / 200,75 / 200,80).
        public const int RowTextLeft = 200;
        public const int RowNameTop = 30;
        public const int RowTitleTop = 55;
        public const int RowDetailTop = 75;
        public const int RowWorldDetailTop = 80;

        // ---- Start menu (ALPHA_CODE_STATIC y/pitch; ALPHA_CAPTURE validated anchor) ----
        public const int MenuItemPitch = 50;
        public const float MenuFirstItemTopFraction = 0.566f;

        // ---- Rows (ALPHA_CODE_STATIC pitch 230, y 50; ALPHA_CAPTURE validated row size/offset) ----
        public const int RowPitch = 230;
        public const int RowBoxHeight = 220;
        public const int RowBoxWidth = 600;
        public const int RowBoxGap = 10;

        // ---- Buttons (ALPHA_CAPTURE validated: BACK/SELECT 198x66, multiplayer/connect 298x66) ----
        public const int ButtonWidth = 198;
        public const int WideButtonWidth = 298;
        public const int ButtonHeight = 66;
        public const int ButtonMargin = 30;

        // ---- Colours (ALPHA_CAPTURE, cross-checked with the 0x3E4CCCCD hover colour of 0x583320) ----
        public static readonly Color NameColor = new Color32(64, 255, 255, 255);    // character / world name
        public static readonly Color TitleColor = new Color32(255, 255, 255, 255);  // level line, button labels
        public static readonly Color DetailColor = new Color32(127, 127, 255, 255); // specialization / seed line
        public static readonly Color HoverColorValue = new Color(0.2f, 1f, 1f, 1f);

        /// <summary>One label of a list row / menu with the Alpha colour. Outline and size come from USS.</summary>
        public static Label Line(VisualElement parent, string text, string name, Color color, int size)
        {
            var label = new Label(text) { name = name };
            label.AddToClassList("alpha-line");
            label.style.fontSize = size;
            label.style.color = color;
            parent.Add(label);
            return label;
        }

        /// <summary>Alpha button: flat dark fill, 2 px white frame, centred white label (ALPHA_CAPTURE).</summary>
        public static Button Button(VisualElement parent, string title, string name, bool wide, System.Action action)
        {
            var button = new Button(action) { text = title, name = name };
            button.AddToClassList("alpha-button");
            if (wide) button.AddToClassList("alpha-button-wide");
            parent.Add(button);
            return button;
        }

        /// <summary>Alpha menu entry of 0x583320 / 0x587710: centred text, no frame, cyan on hover.</summary>
        public static Button MenuItem(VisualElement parent, string title, string name, System.Action action)
        {
            var button = new Button(action) { text = title, name = name };
            button.AddToClassList("alpha-menu-item");
            parent.Add(button);
            return button;
        }
    }
}
