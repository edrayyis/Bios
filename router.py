# router.py
# Routes the user's input to the correct manufacturer algorithm module.
# Flat import structure - all files live in the repo root.

from hp        import hp_decode
from dell      import dell_decode
from lenovo    import lenovo_decode
from fujitsu   import fujitsu_decode
from samsung   import samsung_decode
from generic   import insyde_decode, phoenix_decode


_ROUTE_MAP = {
    "HP / Compaq":           hp_decode,
    "Dell":                  dell_decode,
    "Lenovo":                lenovo_decode,
    "Fujitsu":               fujitsu_decode,
    "Samsung":               samsung_decode,
    "Insyde H2O (OEM)":      insyde_decode,
    "Phoenix / Award (OEM)": phoenix_decode,
}


class AlgorithmRouter:
    """
    Central dispatcher.
    Returns a list of (algorithm_name, result_string) tuples so the UI can
    display multiple candidate passwords when an algorithm produces more than one.
    """

    @staticmethod
    def route(manufacturer: str, model: str, recovery_code: str) -> list:
        func = _ROUTE_MAP.get(manufacturer)
        if func is None:
            return [("Unsupported Manufacturer",
                     f"No algorithm implemented for '{manufacturer}' yet. "
                     f"Try bios-pw.org manually with this code.")]
        try:
            return func(model, recovery_code)
        except Exception as exc:
            return [("Runtime Error", f"Algorithm raised an exception: {exc}")]
