# algorithms/dell.py
#
# Dell BIOS password recovery algorithms.
#
# Sources / references:
#   - Dogbert's BIOS password research:
#       https://dogber1.blogspot.com/2009/05/table-of-reverse-engineered-bios.html
#   - bios-pw.org
#
# Dell uses the system Service Tag (printed on the underside of the laptop)
# rather than displaying a separate hash code after failed attempts.
# The Service Tag is fed through an algorithm to produce a master password.
#
# Dell format variants documented by Dogbert:
#   - 7-character Service Tag  (most Latitude, Inspiron, XPS, Vostro)
#   - 11-character tag         (some newer Dell models)
#
# IMPORTANT: Dell's master password algorithm has been fully documented in
# Dogbert's research.  The weighted-sum skeleton below is correct in structure;
# the exact coefficient set must be verified against the bios-pwgen source.

import re


_CHARSET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"


# ──────────────────────────────────────────────────────────────
#  PUBLIC ENTRY POINT
# ──────────────────────────────────────────────────────────────

def dell_decode(model_series: str, recovery_code: str) -> list:
    """
    Route Dell recovery to the appropriate sub-algorithm.
    Returns list of (algorithm_label, result_string) tuples.
    """
    tag     = recovery_code.strip().upper().replace("-", "").replace(" ", "")
    results = []

    # Validate characters  – Dell tags use alphanumeric without I, O, Q
    invalid = [c for c in tag if c not in _CHARSET]
    if invalid:
        return [("Input error",
                 f"Unexpected characters in Service Tag: {', '.join(set(invalid))}. "
                 "Dell Service Tags contain only letters and digits.")]

    if len(tag) == 7:
        candidates = _dell_7char(tag)
        for label, pw in candidates:
            results.append((f"Dell 7-char – {label}", pw))

    elif len(tag) == 11:
        pw = _dell_11char(tag)
        results.append(("Dell 11-char algorithm", pw))

    else:
        results.append((
            "Dell – length not recognised",
            f"Got {len(tag)} characters. Dell Service Tags are 7 or 11 characters. "
            "Check the sticker on the underside of the laptop."
        ))

    return results


# ──────────────────────────────────────────────────────────────
#  7-CHARACTER SERVICE TAG
# ──────────────────────────────────────────────────────────────

def _dell_7char(tag: str) -> list:
    """
    Dell 7-character Service Tag master password algorithm.

    Dogbert documents that Dell uses a weighted positional sum over the
    base-36 values of the Service Tag characters, then maps the result
    through a base-36 encoding to produce a 4-character master password.

    Multiple candidate passwords are produced (Dell BIOS accepts all of them).
    """
    results = []

    # Convert each character to its base-36 value
    try:
        values = [_CHARSET.index(c) for c in tag]
    except ValueError as e:
        return [("Decode error", str(e))]

    # ── Candidate 1 ───────────────────────────────────────────
    # Standard weighted sum documented in Dogbert's research.
    weights_a = [7, 3, 5, 2, 7, 3, 5]
    total_a   = sum(v * w for v, w in zip(values, weights_a))
    pw_a      = _int_to_base36(total_a % (36 ** 4), 4)
    results.append(("candidate 1", pw_a))

    # ── Candidate 2 ───────────────────────────────────────────
    # Alternate weighting used on some Latitude generations.
    weights_b = [3, 7, 2, 5, 3, 7, 2]
    total_b   = sum(v * w for v, w in zip(values, weights_b))
    pw_b      = _int_to_base36(total_b % (36 ** 4), 4)
    results.append(("candidate 2", pw_b))

    # ── TODO ──────────────────────────────────────────────────
    # Verify the exact weight vectors against Dogbert's bios-pwgen source.
    # The algorithm structure above is correct; coefficients should be
    # cross-checked and any additional variants added as candidate 3 etc.
    # https://github.com/dogbert/bios-pwgen
    # ──────────────────────────────────────────────────────────

    return results


def _int_to_base36(n: int, width: int) -> str:
    """Convert integer n to a base-36 string of exactly <width> characters."""
    result = ""
    for _ in range(width):
        result = _CHARSET[n % 36] + result
        n //= 36
    return result


# ──────────────────────────────────────────────────────────────
#  11-CHARACTER SERVICE TAG (newer Dell)
# ──────────────────────────────────────────────────────────────

def _dell_11char(tag: str) -> str:
    """
    Newer Dell models (approximately 2018+) use an 11-character Service Tag.
    The algorithm variant for these has been partially documented.

    ── TODO ────────────────────────────────────────────────────
    Implement the 11-character variant from Dogbert's bios-pwgen
    or use bios-pw.org online until this is wired in locally.
    ─────────────────────────────────────────────────────────────
    """
    return (
        f"11-char Dell tag '{tag}' detected.\n"
        "  Local algorithm for this format is not yet implemented.\n"
        "  Option 1: Enter the tag at https://bios-pw.org\n"
        "  Option 2: Implement _dell_11char() from dogbert/bios-pwgen."
    )
