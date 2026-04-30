# algorithms/generic.py
#
# Generic OEM BIOS fallback handlers.
#
# Insyde H2O and Phoenix/Award BIOS are firmware platforms used by many OEM
# manufacturers (Acer, Toshiba, Sony VAIO, MSI, Clevo, etc.).  Each OEM
# customises the platform, so there is no single universal algorithm.
#
# This module provides guidance and a known-backdoor lookup rather than
# a computational algorithm, which is appropriate for these cases.


_PHOENIX_BACKDOORS = [
    "phoenix",
    "PHOENIX",
    "CMOS",
    "cmos",
    "BIOS",
    "bios",
    "award",
    "AWARD",
    "lkwpeter",    # very old Award backdoor
    "Polaris",
    "j262",
    "HLT",
    "SER",
    "SKY_FOX",
    "589589",
    "589721",
    "595595",
]


def insyde_decode(model_series: str, recovery_code: str) -> list:
    """
    Insyde H2O BIOS – used by Acer, Toshiba, Sony VAIO, MSI, Clevo and others.
    The hash format and algorithm vary significantly by OEM customisation.
    """
    return [(
        "Insyde H2O OEM BIOS",
        f"OEM: {model_series}\n\n"
        "Insyde H2O recovery is OEM-specific – no single algorithm applies.\n\n"
        "Recommended steps:\n"
        "  1. Go to https://bios-pw.org and enter the code shown on screen.\n"
        "  2. If a serial number is shown instead of a hash, enter that.\n"
        "  3. For Acer/Gateway: try 'Acer' category on bios-pw.org\n"
        "  4. For Toshiba: try 'Toshiba' category on bios-pw.org\n"
        "  5. For Clevo/Sager: try 'Insyde' category on bios-pw.org"
    )]


def phoenix_decode(model_series: str, recovery_code: str) -> list:
    """
    Phoenix / Award BIOS – used by many OEM manufacturers.
    Some very old Phoenix builds have known static backdoor passwords.
    Newer Phoenix BIOS implementations require bios-pw.org or hardware reset.
    """
    results = []

    # Older Phoenix/Award BIOS accepts static backdoor passwords
    # regardless of the user-set password.  Return the list to try.
    backdoor_list = "\n".join(f"  {pw}" for pw in _PHOENIX_BACKDOORS)

    results.append((
        "Phoenix / Award – backdoor passwords to try",
        "Try each of the following passwords on the BIOS lock screen.\n"
        "These are documented static backdoors present in many Award/Phoenix builds:\n\n"
        + backdoor_list
    ))

    results.append((
        "Phoenix / Award – if backdoors fail",
        "If static backdoors do not work, the BIOS has a custom OEM build.\n"
        "  1. Go to https://bios-pw.org and enter the code shown on screen.\n"
        "  2. If no hash is shown, a CMOS battery pull (30+ minutes) may\n"
        "     reset the BIOS password on older systems.\n"
        "  3. Some boards have a CLR_CMOS jumper that achieves the same result."
    ))

    return results
