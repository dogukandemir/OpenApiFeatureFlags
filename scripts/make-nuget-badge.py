#!/usr/bin/env python3
"""Writes the NuGet badge SVG for one version to stdout.

The README serves this badge from the repository itself, not a third-party image host, so the
release workflow regenerates the file from the released tag (see `.github/workflows/release.yml`).
The geometry mirrors the flat shields.io layout it replaces: a grey label pill, a blue value pill,
20px height, 3px corners.
"""

import sys

LABEL = "nuget"
LABEL_BG = "#555"
VALUE_BG = "#004880"  # NuGet blue


def pill_width(text: str) -> int:
    return round(len(text) * 6.6) + 10


def text_pair(x: int, text: str) -> str:
    length = round(len(text) * 66.6)
    return (
        f'<text aria-hidden="true" x="{x}" y="150" fill="#010101" fill-opacity=".3"'
        f' transform="scale(.1)" textLength="{length}">{text}</text>'
        f'<text x="{x}" y="140" transform="scale(.1)" fill="#fff"'
        f' textLength="{length}">{text}</text>'
    )


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: make-nuget-badge.py <version>   e.g. make-nuget-badge.py 0.2.1", file=sys.stderr)
        return 2

    value = "v" + sys.argv[1].removeprefix("v")
    label_width = pill_width(LABEL)
    value_width = pill_width(value)
    total = label_width + value_width

    print(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{total}" height="20" role="img"'
        f' aria-label="{LABEL}: {value}"><title>{LABEL}: {value}</title>'
        '<linearGradient id="s" x2="0" y2="100%">'
        '<stop offset="0" stop-color="#bbb" stop-opacity=".1"/>'
        '<stop offset="1" stop-opacity=".1"/></linearGradient>'
        f'<clipPath id="r"><rect width="{total}" height="20" rx="3" fill="#fff"/></clipPath>'
        f'<g clip-path="url(#r)"><rect width="{label_width}" height="20" fill="{LABEL_BG}"/>'
        f'<rect x="{label_width}" width="{value_width}" height="20" fill="{VALUE_BG}"/>'
        f'<rect width="{total}" height="20" fill="url(#s)"/></g>'
        '<g fill="#fff" text-anchor="middle"'
        ' font-family="Verdana,Geneva,DejaVu Sans,sans-serif"'
        ' text-rendering="geometricPrecision" font-size="110">'
        + text_pair(label_width * 5, LABEL)
        + text_pair(round((label_width + value_width / 2) * 10), value)
        + "</g></svg>"
    )
    print()
    return 0


if __name__ == "__main__":
    sys.exit(main())
