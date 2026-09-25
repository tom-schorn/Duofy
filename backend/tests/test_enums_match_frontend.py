"""The categories in the backend against the ones in the frontend.

`enums.py` is the source: every member carries its group, its label and its
block, so a category without a block cannot be written down at all. The frontend
holds the same list a second time — the German labels and `BLOCK_SUGGESTION` —
and that copy is maintained by hand.

Since #98 the labels live in the translation catalog, `frontend/src/locales/
de.json` under `enums.category`, nested along the dot of the value
(`enums.category.personal.gifts`). `BLOCK_SUGGESTION` stays in `domain.ts`.

That makes it the last place where the two halves can drift apart, and drifting
is silent: a category the frontend does not know renders as a raw value like
`personal.gifts`, and a block that disagrees preselects the wrong one.

So the test reads the frontend files. Unusual for a backend test, and the point
of it: nothing else notices.

Part of #13.
"""

import json
import re
from pathlib import Path

import pytest

from app.models.enums import Category

FRONTEND = Path(__file__).parents[2] / "frontend" / "src"
DOMAIN = FRONTEND / "lib" / "domain.ts"
CATALOG = FRONTEND / "locales" / "de.json"


def table(name: str) -> dict[str, str]:
    """One `Record<Category, …>` from the frontend, as a dictionary."""
    source = DOMAIN.read_text(encoding="utf-8")
    match = re.search(rf"export const {name}[^{{]*\{{(.*?)\n\}}", source, re.S)
    assert match, f"{name} not found in domain.ts"
    return dict(re.findall(r"'([a-z]+\.[a-z_]+)': '([^']+)'", match.group(1)))


@pytest.fixture(scope="module")
def labels() -> dict[str, str]:
    """The German category labels from the catalog, flattened back to the values.

    `{"personal": {"gifts": "…"}}` becomes `{"personal.gifts": "…"}` — the same
    shape the categories have in the backend. A value without a dot would stand
    on its own, as a plain string.
    """
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    labels: dict[str, str] = {}
    for group, entries in catalog["enums"]["category"].items():
        if isinstance(entries, str):
            labels[group] = entries
        else:
            labels.update({f"{group}.{name}": label for name, label in entries.items()})
    return labels


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
