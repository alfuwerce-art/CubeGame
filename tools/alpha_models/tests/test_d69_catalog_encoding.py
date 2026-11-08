"""Issue #4 review fix: the D69 catalog generator and its two Markdown registries must be readable UTF-8.

The published D69 generator carried mojibake in its own string literals (UTF-8 Cyrillic/dashes decoded as
cp1251 and saved again: 'РЎРІРѕРґРєР°' for 'Сводка', 'вЂ”' for '—'), so every regeneration reproduced it.
These checks run on the committed files only (no Alpha data needed)."""
from __future__ import annotations

import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
GENERATOR = ROOT / "tools" / "alpha_models" / "catalog" / "d69_catalogs.py"
DOC_A = ROOT / "docs" / "research" / "alpha2013" / "S05B2_ALPHA_ORIGINAL_MODEL_ANIMATION_CATALOG.md"
DOC_B = ROOT / "docs" / "research" / "alpha2013" / "S05B2_UNITY_TRANSFER_DISPOSITION_MATRIX.md"

# Signatures of UTF-8 text decoded as cp1251: 'Р'/'С' followed by a cp1251 punctuation/Serbian letter that
# never follows them in Russian, the decoded em/en dash or quote prefix 'вЂ', and raw C1 control characters.
MOJIBAKE = re.compile(r"[\x80-\x9f]"
                      r"|Р[ЃЂ‚ѓ„…†‡€‰Љ‹ЊЌЋЏђ‘’“”•–—™љ›њќћџЎўЈ¤Ґ¦§Ё©Є«¬®Їµ¶·°±]"
                      r"|С[Ђ‚ѓ„…†‡€‰Љ‹ЊЌЋЏ]"
                      r"|вЂ[”“™¦\x9c\x9d\x93\x94]")


def _bad_lines(path: Path):
    text = path.read_text(encoding="utf-8")           # must also be valid UTF-8
    return [(i, line[:120]) for i, line in enumerate(text.splitlines(), 1) if MOJIBAKE.search(line)]


class D69CatalogEncoding(unittest.TestCase):
    def test_detector_recognises_mojibake(self):
        for sample in ("РЎРІРѕРґРєР°", "РєР°С‚Р°Р»РѕРі", "D69 вЂ” x"):
            self.assertRegex(sample, MOJIBAKE)
        for sample in ("Сводка", "каталог ОРИГИНАЛЬНОЙ Alpha", "D69 — матрица", "Ряд, Рёбра, Сюжет, Сёла"):
            self.assertNotRegex(sample, MOJIBAKE)

    def test_generator_literals_are_clean(self):
        self.assertEqual(_bad_lines(GENERATOR), [])

    def test_generated_documents_are_clean(self):
        for doc in (DOC_A, DOC_B):
            with self.subTest(doc=doc.name):
                self.assertEqual(_bad_lines(doc), [])

    def test_readable_headers(self):
        a = DOC_A.read_text(encoding="utf-8").splitlines()
        b = DOC_B.read_text(encoding="utf-8").splitlines()
        self.assertEqual(a[0], "# S05B-2 / D69 — каталог ОРИГИНАЛЬНОЙ Alpha 2013: модели, существа, объекты "
                               "и их анимации")
        self.assertTrue(b[0].startswith("# S05B-2 / D69 — матрица переноса Alpha → Unity"))
        for heading in ("## Сводка", "## Ключевые оригинальные факты, найденные в D69 (ALPHA_CODE)",
                        "## Живая перепись (запущенная оригинальная игра)", "## Каталог по видам"):
            self.assertIn(heading, a)
        for heading in ("## Сводка", "## Не KEEP (всё, что требует действия)", "## KEEP (перенесено верно)"):
            self.assertIn(heading, b)

    def test_generator_writes_utf8(self):
        src = GENERATOR.read_text(encoding="utf-8")
        for call in re.findall(r"write_text\([^\n]*", src):
            self.assertIn('encoding="utf-8"', call)


if __name__ == "__main__":
    unittest.main()
