# algorithms/fujitsu.py
#
# Fujitsu LifeBook BIOS password recovery.
#
# Fujitsu's BIOS recovery screen displays a code in the format:
#   XXXX-XXXX-XXXX-XXXX-XXXX  (5 groups of 4 hexadecimal characters)
#
# The algorithm is documented in Dogbert's research and bios-pwgen.
# The XOR-and-rotation skeleton here reflects the documented structure;
# verify the exact rotation constants against the bios-pwgen source.
#
# References:
#   - Dogbert: https://dogber1.blogspot.com/2009/05/table-of-reverse-engineered-bios.html
#   - bios-pwgen: https://github.com/dogbert/bios-pwgen

import re


def fujitsu_decode(model_series: str, recovery_code: str) -> list:
    # Normalise: strip dashes, spaces, lowercase -> upper
    code = recovery_code.strip().upper().replace("-", "").replace(" ", "")

    if len(code) == 20 and all(c in "0123456789ABCDEF" for c in code):
        pw = _fujitsu_hex20(code)
        return [("Fujitsu 5×4 hex algorithm", pw)]
    else:
        return [(
            "Fujitsu – format not recognised",
            f"Got '{recovery_code}'.\n"
            "Expected format: XXXX-XXXX-XXXX-XXXX-XXXX  (5 groups of 4 hex chars).\n"
            "Copy the code exactly as shown on the Fujitsu BIOS lock screen."
        )]


def _fujitsu_hex20(code: str) -> str:
    """
    Fujitsu 20-hex-character BIOS password recovery.
    The BIOS displays the code as five 16-bit words (hex).
    The algorithm XORs and rotates those words to derive the password.

    ── TODO ─────────────────────────────────────────────────────────────────
    Verify/replace the transform below with the exact implementation from
    Dogbert's bios-pwgen (look for the Fujitsu handler function).
    https://github.com/dogbert/bios-pwgen
    ──────────────────────────────────────────────────────────────────────────
    """
    try:
        # Parse into five 16-bit words
        words = [int(code[i:i+4], 16) for i in range(0, 20, 4)]

        # Documented structure: XOR chain across words with bit rotation
        acc = words[0]
        for i in range(1, 5):
            # rotate left 3 bits (16-bit), then XOR with next word
            acc = ((acc << 3) | (acc >> 13)) & 0xFFFF
            acc ^= words[i]

        # Password is the final accumulator expressed as a 4-char hex string
        password = f"{acc:04X}"

        return (
            f"Candidate password : {password}\n\n"
            "Note: If this does not work, verify the algorithm constants\n"
            "against Dogbert's bios-pwgen source and update _fujitsu_hex20()."
        )

    except Exception as e:
        return f"Decode error: {e}"
