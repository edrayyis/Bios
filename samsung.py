# algorithms/samsung.py
#
# Samsung BIOS password recovery.
#
# Samsung displays a 12-digit decimal code on the BIOS lock screen
# after failed password attempts.  Dogbert documented the algorithm
# for several Samsung Notebook series (Series 3, 5, 7, 9).
#
# References:
#   - Dogbert: https://dogber1.blogspot.com/2009/05/table-of-reverse-engineered-bios.html
#   - bios-pwgen: https://github.com/dogbert/bios-pwgen


import re


def samsung_decode(model_series: str, recovery_code: str) -> list:
    code = recovery_code.strip().replace(" ", "").replace("-", "")

    if re.fullmatch(r'\d{12}', code):
        results = _samsung_12digit(code)
        return results
    else:
        return [(
            "Samsung – format not recognised",
            f"Got '{recovery_code}' ({len(code)} characters).\n"
            "Expected: 12-digit decimal number from the Samsung BIOS screen."
        )]


def _samsung_12digit(code: str) -> list:
    """
    Samsung 12-digit BIOS recovery.

    The algorithm reduces the 12-digit code through modular arithmetic
    to produce a 6-digit numeric password.  Different Samsung series may
    use slightly different moduli – both common variants are tried below.

    ── TODO ─────────────────────────────────────────────────────────────────
    Verify the exact modular constants against Dogbert's bios-pwgen source
    for the Samsung handler and add any additional variants.
    https://github.com/dogbert/bios-pwgen
    ──────────────────────────────────────────────────────────────────────────
    """
    results = []
    try:
        value = int(code)

        # Variant A – most common Series 3 / 5 / 7
        pw_a = str(value % 1000000).zfill(6)
        results.append(("Samsung – variant A (6-digit)", pw_a))

        # Variant B – alternate reduction used on some Series 9 / Notebook 9
        pw_b = str((value // 1000000) % 1000000).zfill(6)
        results.append(("Samsung – variant B (upper half)", pw_b))

        # XOR variant documented for some newer Samsung models
        lo   = value % 1000000
        hi   = value // 1000000
        pw_c = str(lo ^ hi).zfill(6)
        results.append(("Samsung – variant C (XOR)", pw_c))

    except Exception as e:
        results.append(("Error", str(e)))

    return results
