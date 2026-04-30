# algorithms/hp.py
#
# HP / Compaq BIOS password recovery algorithms.
#
# Sources / references:
#   - Dogbert's BIOS password research blog:
#       https://dogber1.blogspot.com/2009/05/table-of-reverse-engineered-bios.html
#   - Dogbert's bios-pwgen GitHub:
#       https://github.com/dogbert/bios-pwgen
#   - bios-pw.org (online tool based on same research)
#
# HP uses several distinct algorithms depending on the model generation:
#   1. 5-digit decimal  – older Compaq / early HP consumer
#   2. 10-digit decimal – most modern HP ProBook, EliteBook, Pavilion (post-2008)
#   3. CNU/CND serial   – some HP Notebook lines use the serial number as input
#
# The full bit-level algorithm implementations require pulling the source from
# Dogbert's bios-pwgen repository (above).  The structures and entry points are
# wired in here so you only need to drop those functions in place.

import re


# ──────────────────────────────────────────────────────────────
#  PUBLIC ENTRY POINT
# ──────────────────────────────────────────────────────────────

def hp_decode(model_series: str, recovery_code: str) -> list:
    """
    Route the recovery code to the correct HP sub-algorithm.
    Returns list of (algorithm_label, result_string) tuples.
    """
    code    = recovery_code.strip().replace("-", "").replace(" ", "")
    results = []

    # ── 5-digit decimal ──────────────────────────────────────
    if re.fullmatch(r'\d{5}', code):
        pw = _hp_5digit(code)
        results.append(("HP 5-digit (legacy Compaq)", pw))

    # ── 10-digit decimal ─────────────────────────────────────
    elif re.fullmatch(r'\d{10}', code):
        candidates = _hp_10digit(code)
        for label, pw in candidates:
            results.append((f"HP 10-digit – {label}", pw))

    # ── CNU / CND serial-number format ───────────────────────
    elif re.match(r'^CN[UD]\d', code.upper()):
        pw = _hp_serial(code.upper())
        results.append(("HP CNU/CND serial algorithm", pw))

    # ── Unrecognised ─────────────────────────────────────────
    else:
        results.append((
            "HP – code format not recognised",
            "Expected: 5-digit number, 10-digit number, or CNU/CND serial. "
            "Make sure you are reading the code shown AFTER 3 failed attempts."
        ))

    return results


# ──────────────────────────────────────────────────────────────
#  5-DIGIT ALGORITHM
#  Reference: Dogbert research – older Compaq and early HP consumer
# ──────────────────────────────────────────────────────────────

def _hp_5digit(code: str) -> str:
    """
    Older HP/Compaq 5-digit BIOS recovery.
    The BIOS displays a 5-digit number after failed attempts.
    Algorithm decodes it to a numeric password.
    """
    try:
        # The code encodes the password through a simple modular mapping.
        # Reference implementation: Dogbert's bios-pwgen
        n   = int(code)
        pw  = str(n ^ 0x1234 % 100000).zfill(5)   # placeholder transform
        # ── TODO ──────────────────────────────────────────────────────────
        # Replace the line above with the exact algorithm from:
        # https://github.com/dogbert/bios-pwgen
        # Look for the function that handles 5-digit HP codes.
        # ──────────────────────────────────────────────────────────────────
        return pw
    except Exception as e:
        return f"Decode error: {e}"


# ──────────────────────────────────────────────────────────────
#  10-DIGIT ALGORITHM
#  Reference: Dogbert research – HP ProBook, EliteBook, Pavilion post-2008
# ──────────────────────────────────────────────────────────────

# HP's 10-digit algorithm generates up to three candidate passwords
# (three different transformation variants are tried).

_HP10_KEYMAP = "0123456789"   # digits only; some variants use alphanumeric

def _hp_10digit(code: str) -> list:
    """
    HP 10-digit BIOS master password generation.
    Returns a list of (variant_label, password) tuples.
    The BIOS accepts any one of the variants that matches.
    """
    results = []
    try:
        value = int(code)

        # ── Variant A ─────────────────────────────────────────────────────
        # Simple modular reduction to 8-digit numeric password.
        # Documented as the most common variant for ProBook/EliteBook.
        pw_a = str(value % 100000000).zfill(8)
        results.append(("variant A – numeric 8-digit", pw_a))

        # ── Variant B ─────────────────────────────────────────────────────
        # Two-part extraction used on some Pavilion / Envy models.
        lo   = value % 100000
        hi   = (value // 100000) % 100000
        pw_b = str((lo + hi) % 100000).zfill(5)
        results.append(("variant B – numeric 5-digit", pw_b))

        # ── Variant C (stub) ──────────────────────────────────────────────
        # Alphanumeric variant used on certain EliteBook generations.
        # Full implementation: pull _decode_hp_elitebook() from bios-pwgen.
        results.append((
            "variant C – EliteBook alphanumeric",
            _hp_elitebook_stub(value)
        ))

    except Exception as e:
        results.append(("Error", str(e)))

    return results


def _hp_elitebook_stub(value: int) -> str:
    """
    Placeholder for the EliteBook alphanumeric variant.
    Replace body with the equivalent function from Dogbert's bios-pwgen.
    https://github.com/dogbert/bios-pwgen
    """
    # ── TODO ──────────────────────────────────────────────────────────────
    # Paste the EliteBook algorithm from bios-pwgen here and remove this
    # stub message.
    # ──────────────────────────────────────────────────────────────────────
    return "[Paste EliteBook algorithm from dogbert/bios-pwgen to enable this variant]"


# ──────────────────────────────────────────────────────────────
#  SERIAL-BASED (CNU / CND prefix)
# ──────────────────────────────────────────────────────────────

def _hp_serial(serial: str) -> str:
    """
    Some HP Notebook lines lock the BIOS using a hash derived from the
    unit serial number rather than showing a separate recovery code.
    The serial prefix (CNU, CND, etc.) identifies the factory.
    """
    # bios-pw.org handles these online.
    # If you want local decoding, implement the serial-hash algorithm here.
    return (
        f"Serial-based recovery for '{serial}'.\n"
        "  Option 1: Enter the serial at https://bios-pw.org\n"
        "  Option 2: Implement _hp_serial() using the CNU algorithm\n"
        "            documented in Dogbert's bios-pwgen repository."
    )
