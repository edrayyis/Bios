# algorithms/lenovo.py
#
# Lenovo BIOS password recovery.
#
# ThinkPad business-class models store the supervisor password in an
# EEPROM chip (24RF08 or similar) on the motherboard.  The BIOS itself
# does not expose a recovery hash to the user in the same way HP and
# Dell do.  Recovery typically requires one of:
#   a) A hardware programmer to read / blank the EEPROM directly.
#   b) Lenovo's enterprise support channel with proof of ownership.
#   c) Third-party ThinkPad-specific tools that communicate with the chip.
#
# IdeaPad / Yoga / Legion consumer models:
#   These models often tie the BIOS to a Lenovo account or use a
#   challenge-response mechanism.  No universal software-only algorithm
#   has been publicly documented for these lines.
#
# References:
#   - ThinkWiki: https://www.thinkwiki.org/wiki/Problem_with_Supervisor_Password
#   - Dogbert's research (limited Lenovo coverage):
#       https://dogber1.blogspot.com/2009/05/table-of-reverse-engineered-bios.html


import re


def lenovo_decode(model_series: str, recovery_code: str) -> list:
    code = recovery_code.strip()

    if "ThinkPad" in model_series:
        return _thinkpad(model_series, code)
    else:
        return _consumer_lenovo(model_series, code)


def _thinkpad(model_series: str, code: str) -> list:
    """
    ThinkPad supervisor password is stored in hardware.
    Software-only recovery is not possible without EEPROM access.
    """
    return [(
        "Lenovo ThinkPad – hardware EEPROM",
        "ThinkPad supervisor passwords are stored in a dedicated EEPROM chip,\n"
        "not recoverable via software algorithm alone.\n\n"
        "Recovery options:\n"
        "  1. Hardware: Read/blank the 24RF08 (or similar) EEPROM with a\n"
        "     programmer attached to the chip via the keyboard connector.\n"
        "  2. Lenovo enterprise support with proof of purchase.\n"
        "  3. Third-party ThinkPad EEPROM service (check ThinkWiki)."
    )]


def _consumer_lenovo(model_series: str, code: str) -> list:
    return [(
        "Lenovo IdeaPad / Yoga / Legion",
        "Consumer Lenovo models do not have a published universal algorithm.\n"
        "Try:\n"
        "  1. bios-pw.org with any hash code shown on the lock screen.\n"
        "  2. Lenovo support at https://support.lenovo.com"
    )]
