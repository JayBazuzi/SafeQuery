#!/usr/bin/env python3
"""Create a commit message: <risk char> <intention char> <message>

Usage: create_message.py RISK INTENTION MESSAGE
See InternalDocuments/Commit.md for the names.
"""
import sys

RISKS = {
    "completely_safe": ".",
    "proven": "-",
    "single_item": "!",
    "other": "@",
}

INTENTIONS = {
    "environment": "e",
    "test": "t",
    "refactor": "r",
    "feature": "F",
    "bug": "B",
    "documentation": "d",
}

# Only these intentions change code behavior, so only they may carry a risk
# other than completely_safe.
CODE_INTENTIONS = {"refactor", "feature", "bug"}


def create_message(risk, intention, message):
    if risk not in RISKS:
        raise ValueError(f"unrecognized risk '{risk}' (expected one of: {', '.join(RISKS)})")
    if intention not in INTENTIONS:
        raise ValueError(f"unrecognized intention '{intention}' (expected one of: {', '.join(INTENTIONS)})")
    if intention not in CODE_INTENTIONS and risk != "completely_safe":
        raise ValueError(f"intention '{intention}' is not a code change, so risk must be completely_safe")
    if not message or not message.strip():
        raise ValueError("message is required")
    return f"{RISKS[risk]} {INTENTIONS[intention]} {message.strip()}"


def main(argv):
    if len(argv) != 4:
        print("usage: create_message.py risk intention message", file=sys.stderr)
        return 2
    try:
        print(create_message(*argv[1:]))
    except ValueError as e:
        print(f"error: {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
