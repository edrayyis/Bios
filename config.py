# config.py
# Manufacturer definitions, model lists, and hash format hints.

MANUFACTURERS = {
    "HP / Compaq": {
        "models": [
            "ProBook", "EliteBook", "Pavilion", "Envy", "Spectre",
            "Stream", "ZBook", "Compaq Presario", "Other"
        ],
        "hash_format": (
            "5-digit number  OR  10-digit number displayed on screen "
            "after 3 failed password attempts."
        ),
        "algorithm": "hp"
    },
    "Dell": {
        "models": [
            "Latitude", "Inspiron", "XPS", "Precision",
            "Vostro", "Studio", "Alienware", "Other"
        ],
        "hash_format": (
            "7-character Service Tag (e.g. ABC1234)  OR  11-character tag. "
            "Found on a sticker on the underside of the laptop."
        ),
        "algorithm": "dell"
    },
    "Lenovo": {
        "models": [
            "ThinkPad T-Series", "ThinkPad X-Series", "ThinkPad L-Series",
            "ThinkPad E-Series", "IdeaPad", "Yoga", "Legion", "Other"
        ],
        "hash_format": (
            "ThinkPad: hash code shown after failed attempts. "
            "IdeaPad and consumer models typically require Lenovo support recovery."
        ),
        "algorithm": "lenovo"
    },
    "Fujitsu": {
        "models": [
            "LifeBook E-Series", "LifeBook S-Series", "LifeBook T-Series",
            "LifeBook U-Series", "CELSIUS", "Other"
        ],
        "hash_format": (
            "5 groups of 4 hex characters, e.g.  A1B2-C3D4-E5F6-A7B8-C9D0  "
            "displayed on the BIOS recovery screen."
        ),
        "algorithm": "fujitsu"
    },
    "Samsung": {
        "models": [
            "Series 3", "Series 5", "Series 7", "Series 9",
            "Notebook 9", "Notebook 7", "Galaxy Book", "Other"
        ],
        "hash_format": (
            "12-digit decimal number shown on the BIOS password screen."
        ),
        "algorithm": "samsung"
    },
    "Insyde H2O (OEM)": {
        "models": [
            "Acer / Gateway", "Toshiba", "Sony VAIO",
            "MSI", "Clevo / Sager", "Other OEM"
        ],
        "hash_format": (
            "Format varies by OEM. Try bios-pw.org with the hash code or "
            "serial number shown on the BIOS lock screen."
        ),
        "algorithm": "insyde"
    },
    "Phoenix / Award (OEM)": {
        "models": [
            "Various OEM", "Other"
        ],
        "hash_format": (
            "Format varies by OEM. Phoenix-based BIOS often accepts a "
            "backdoor password. Try: phoenix, CMOS, BIOS, or bios-pw.org."
        ),
        "algorithm": "phoenix"
    },
}
