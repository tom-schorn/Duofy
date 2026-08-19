"""The categories in the backend against the ones in the frontend.

`enums.py` is the source: every member carries its group, its label and its
block, so a category without a block cannot be written down at all. The frontend
holds the same list a second time — the German labels and `BLOCK_SUGGESTION` —
and that copy is maintained by hand.

That makes it the last place where the two halves can drift apart, and drifting
is silent: a category the frontend does not know renders as a raw value like
`personal.gifts`, and a block that disagrees preselects the wrong one.

So the test reads the TypeScript file. Unusual for a backend test, and the point
of it: nothing else notices.

Part of #13.
"""

import re
from pathlib import Path

import pytest

from app.models.enums import Category

DOMAIN = Path(__file__).parents[2] / "frontend" / "src" / "lib" / "domain.ts"


def table(name: str) -> dict[str, str]:
    """One `Record<Category, …>` from the frontend, as a dictionary."""
    source = DOMAIN.read_text(encoding="utf-8")
    match = re.search(rf"export const {name}[^{{]*\{{(.*?)\n\}}", source, re.S)
    assert match, f"{name} not found in domain.ts"
    return dict(re.findall(r"'([a-z]+\.[a-z_]+)': '([^']+)'", match.group(1)))


@pytest.fixture(scope="module")
def labels() -> dict[str, str]:
    return table("CATEGORY_LABEL")


@pytest.fixture(scope="module")
def suggestions() -> dict[str, str]:
    return table("BLOCK_SUGGESTION")


def test_every_category_has_a_label(labels):
    """A missing label shows the user the raw value: `personal.gifts`."""
    assert {category.value for category in Category} - set(labels) == set()


def test_every_category_has_a_block_suggestion(suggestions):
    assert {category.value for category in Category} - set(suggestions) == set()


def test_the_frontend_invents_nothing(labels, suggestions):
    """A category only the frontend knows fails on the backend as a 422."""
    known = {category.value for category in Category}
    assert (set(labels) | set(suggestions)) - known == set()


def test_the_blocks_agree(suggestions):
    """Where they differ, the form preselects a block the backend disagrees with.

    Nothing breaks — the user can correct it — but the suggestion is wrong
    exactly where it is trusted most, on categories nobody thinks about.
    """
    differing = {
        category.value: (category.block.value, suggestions[category.value])
        for category in Category
        if suggestions.get(category.value) != category.block.value
    }
    assert differing == {}
