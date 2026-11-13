using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// P6 guard: Unity rejects a whole USS file for one unsupported selector, and the game then shows default
    /// full-width buttons. Before P6 a sibling combinator ('+') did exactly that to every Alpha menu style.
    /// </summary>
    public sealed class FullM0AlphaInterfaceStyleTests
    {
        private const string StylePath = "Assets/CubeGame/Presentation/Gameplay/Resources/GameInterface.uss";

        [Test]
        public void GameInterfaceStyleSheetUsesOnlySupportedSelectors()
        {
            string text = Regex.Replace(File.ReadAllText(StylePath), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            foreach (string block in text.Split('}'))
            {
                int brace = block.IndexOf('{');
                if (brace < 0) continue;
                string selector = block.Substring(0, brace);
                Assert.IsFalse(Regex.IsMatch(selector, @"[+~]"), $"sibling combinator in '{selector.Trim()}'");
                Assert.IsFalse(Regex.IsMatch(selector, @":(first-child|last-child|nth-|not\()"),
                    $"unsupported pseudo-class in '{selector.Trim()}'");
            }
        }

        [Test]
        public void GameInterfaceStyleSheetImportsWithoutErrors()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            Assert.IsNotNull(sheet, StylePath);
            PropertyInfo errors = typeof(StyleSheet).GetProperty("importedWithErrors",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (errors != null)
            {
                Assert.IsFalse((bool)errors.GetValue(sheet), "GameInterface.uss imported with errors");
            }

            FieldInfo selectors = typeof(StyleSheet).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(field => field.Name.Contains("complexSelectors"));
            if (selectors?.GetValue(sheet) is System.Array array)
            {
                Assert.Greater(array.Length, 100, "the Alpha rules are present in the imported sheet");
            }
        }
    }
}
