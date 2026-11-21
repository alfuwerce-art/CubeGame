import tempfile
import unittest
from pathlib import Path

from verify_character_docs import check_phases, current_statements, markdown_links, read_documents


class CharacterDocsChecks(unittest.TestCase):
    def test_detects_reopened_mannequin_with_or_without_name(self):
        self.assertTrue(list(current_statements("memo.md", "Визуальная приёмка открыта.")))
        self.assertTrue(list(current_statements("memo.md", "HUMAN_MALE_BASE_V1 — ACCEPTED.")))
        self.assertFalse(list(current_statements("memo.md", "HUMAN_MALE_BASE_V1 — REVIEW_CLOSED / NOT_SELECTED.")))

    def test_distinguishes_archive_and_quoted_history(self):
        text = "> Историческая запись: HUMAN_MALE_BASE_V1 — ACCEPTED.\n\nТекущий статус закрыт."
        self.assertFalse(list(current_statements("memo.md", text)))
        self.assertTrue(list(current_statements("stage.md", "# ACTIVE STAGE — M0 ALPHA PORT")))
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "old.md").write_text("> Архив.\n\nHUMAN_MALE_BASE_V1 — ACCEPTED.", encoding="utf-8")
            _, errors = read_documents(root, (), ("old.md",))
            self.assertEqual(errors, [])

    def test_phase_is_compared_without_freezing_future_stage_names(self):
        first = "**Код этапа** `FUTURE_STAGE`\n**Результат ревизии** `IN_PROGRESS`\n"
        self.assertEqual(check_phases([("one.md", first), ("two.md", first)]), [])
        second = first.replace("FUTURE_STAGE", "OTHER_STAGE")
        self.assertTrue(check_phases([("one.md", first), ("two.md", second)]))
        self.assertTrue(check_phases([("one.md", first + first)]))

    def test_links_validate_paths_and_fragments(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "target.md").write_text("# Старт и цели\n", encoding="utf-8")
            self.assertFalse(list(markdown_links(root, "readme.md", "[x](target.md#старт-и-цели)")))
            self.assertTrue(list(markdown_links(root, "readme.md", "[x](target.md#missing)")))
            self.assertTrue(list(markdown_links(root, "readme.md", "[x](missing.md)")))


if __name__ == "__main__":
    unittest.main()
