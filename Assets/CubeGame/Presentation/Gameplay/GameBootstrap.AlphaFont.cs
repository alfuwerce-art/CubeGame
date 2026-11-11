using UnityEngine;
using UnityEngine.UIElements;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Alpha UI font. Alpha renders every interface string with the bitmap font loaded from the local file
    /// <c>resource1.dat</c> (ALPHA_CODE_STATIC: <c>FontCache_find_or_create_scalable(L"resource1.dat")</c> in
    /// StartMenuWidget 0x583320, SystemWidget 0x587710, OptionsWidget, Interface, World, Chat, Speech and every
    /// tooltip). The file is a TrueType font whose lowercase glyphs are small capitals, which is why Alpha text
    /// reads as capitals.
    ///
    /// The font is a closed Alpha asset, so it is never committed: the local staging step
    /// (<c>tools/alpha_content/stage_alpha_ui_font.ps1</c>) copies it from the read-only Alpha installation into
    /// <c>Resources/AlphaFont.ttf</c> before a Unity build, where it is imported as a normal Unity font and ends up
    /// inside the user's local Player only. Without the staged file the UI falls back to the placeholder font and
    /// this class reports <c>ALPHA_FONT=ABSENT</c>, so a build without the local Alpha resource is visible in the log
    /// instead of silently looking wrong.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private Font _alphaFont;

        /// <summary>ALPHA_FONT_RUNTIME — RUNTIME when the staged Alpha font is in use, ABSENT otherwise.</summary>
        public string AlphaFontRuntime { get; private set; } = "NOT_APPLIED";

        /// <summary>Applies the staged Alpha UI font to the whole interface (UI Toolkit inherits the font definition).</summary>
        private void ApplyAlphaFont()
        {
            _alphaFont = Resources.Load<Font>(AlphaStartupPresentation.FontResourceName);
            if (_alphaFont == null)
            {
                AlphaFontRuntime = "ABSENT";
                Debug.LogWarning(
                    "[UI] ALPHA_FONT=ABSENT " +
                    $"Resources/{AlphaStartupPresentation.FontResourceName} is not staged; the interface keeps the " +
                    "placeholder font. Run tools/alpha_content/stage_alpha_ui_font.ps1 before building.");
                return;
            }

            _interface.style.unityFontDefinition = new StyleFontDefinition(_alphaFont);
            AlphaFontRuntime = "RUNTIME";
            Debug.Log($"[UI] ALPHA_FONT=RUNTIME source={AlphaStartupPresentation.FontSourceFile} " +
                      $"name='{_alphaFont.name}'");
        }
    }
}
