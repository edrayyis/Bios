# dell.py
#
# Dell BIOS password recovery algorithms.
#
# Sources / references:
#   - Dogbert's BIOS password research:
#       https://dogber1.blogspot.com/2009/05/table-of-reverse-engineered-bios.html
#   - bios-pw.org
#
# Dell input formats:
#   - 7-character Service Tag  (e.g. C7NN6M3)
#   - 11-character tag         (e.g. C7NN6M3-8FC8 or C7NN6M38FC8)

import re

_CHARSET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"


def dell_decode(model_series: str, recovery_code: str) -> list:
    tag     = recovery_code.strip().upper().replace("-", "").replace(" ", "")
    results = []

    invalid = [c for c in tag if c not in _CHARSET]
    if invalid:
        return [("Input error",
                 f"Unexpected characters: {', '.join(set(invalid))}. "
                 "Dell Service Tags contain only letters and digits.")]

    if len(tag) == 7:
        candidates = _dell_7char(tag)
        for label, pw in candidates:
            results.append((f"Dell 7-char - {label}", pw))

    elif len(tag) == 11:
        base   = tag[:7]
        suffix = tag[7:]
        candidates = _dell_11char(base, suffix)
        for label, pw in candidates:
            results.append((f"Dell 11-char - {label}", pw))

    else:
        results.append((
            "Dell - length not recognised",
            f"Got {len(tag)} characters. Dell Service Tags are 7 or 11 characters.\n"
            "If you see a dash in the tag (e.g. C7NN6M3-8FC8) enter the full\n"
            "string including the part after the dash."
        ))

    return results


def _dell_7char(tag: str) -> list:
    results = []
    try:
        values = [_CHARSET.index(c) for c in tag]
    except ValueError as e:
        return [("Decode error", str(e))]

    weights_a = [7, 3, 5, 2, 7, 3, 5]
    total_a   = sum(v * w for v, w in zip(values, weights_a))
    pw_a      = _int_to_base36(total_a % (36 ** 4), 4)
    results.append(("candidate 1", pw_a))

    weights_b = [3, 7, 2, 5, 3, 7, 2]
    total_b   = sum(v * w for v, w in zip(values, weights_b))
    pw_b      = _int_to_base36(total_b % (36 ** 4), 4)
    results.append(("candidate 2", pw_b))

    return results


def _dell_11char(base: str, suffix: str) -> list:
    results = []

    try:
        base_values = [_CHARSET.index(c) for c in base]
    except ValueError as e:
        return [("Decode error", str(e))]

    try:
        suffix_int = int(suffix, 16)
    except ValueError:
        suffix_int = sum(_CHARSET.index(c) for c in suffix)

    # Candidate 1: base tag only with standard weights
    weights_a = [7, 3, 5, 2, 7, 3, 5]
    total_a   = sum(v * w for v, w in zip(base_values, weights_a))
    pw_a      = _int_to_base36(total_a % (36 ** 4), 4)
    results.append(("candidate 1 (base tag)", pw_a))

    # Candidate 2: suffix XOR modification
    total_b = (total_a ^ suffix_int) % (36 ** 4)
    pw_b    = _int_to_base36(total_b, 4)
    results.append(("candidate 2 (suffix XOR)", pw_b))

    # Candidate 3: full 11-char extended polynomial
    all_chars = base + suffix
    weights_c = [7, 3, 5, 2, 7, 3, 5, 2, 7, 3, 5]
    try:
        all_values = [_CHARSET.index(c) for c in all_chars]
        total_c    = sum(v * w for v, w in zip(all_values, weights_c))
        pw_c       = _int_to_base36(total_c % (36 ** 4), 4)
        results.append(("candidate 3 (11-char poly)", pw_c))
    except ValueError:
        pass

    # Candidate 4: alternate base weights with suffix added
    weights_d = [3, 7, 2, 5, 3, 7, 2]
    total_d   = sum(v * w for v, w in zip(base_values, weights_d))
    total_d   = (total_d + suffix_int) % (36 ** 4)
    pw_d      = _int_to_base36(total_d, 4)
    results.append(("candidate 4 (alt + suffix)", pw_d))

    results.append((
        "note",
        "Try all 4 candidates on the BIOS lock screen.\n"
        "Cross-check at https://bios-pw.org if none work."
    ))

    return results


def _int_to_base36(n: int, width: int) -> str:
    result = ""
    for _ in range(width):
        result = _CHARSET[n % 36] + result
        n //= 36
    return result
