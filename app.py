"""
BIOS Recovery Manager  -  Streamlit Web App
All files live in the repo root (flat structure).
"""

import streamlit as st
import datetime
import sys
import os

# Ensure current directory is on the path so flat imports work on Streamlit Cloud
sys.path.insert(0, os.path.dirname(__file__))

from router import AlgorithmRouter
from config import MANUFACTURERS

# -- Page config --------------------------------------------------------------
st.set_page_config(
    page_title="BIOS Recovery Manager",
    page_icon="🔓",
    layout="wide",
    initial_sidebar_state="collapsed"
)

# -- Custom CSS ---------------------------------------------------------------
st.markdown("""
<style>
  @import url('https://fonts.googleapis.com/css2?family=Share+Tech+Mono&family=Inter:wght@400;600&display=swap');

  html, body, [class*="css"] {
    font-family: 'Inter', sans-serif;
    background-color: #080d14;
    color: #c8d8e8;
  }

  .app-header {
    background: linear-gradient(90deg, #0a0f1e 0%, #0d1a2e 100%);
    border-bottom: 1px solid #0a2040;
    padding: 18px 28px 14px 28px;
    margin: -1rem -1rem 1.6rem -1rem;
  }
  .app-title {
    font-family: 'Share Tech Mono', monospace;
    font-size: 1.7rem;
    color: #00d4ff;
    letter-spacing: 2px;
    margin: 0;
  }
  .app-subtitle {
    font-family: 'Share Tech Mono', monospace;
    font-size: 0.75rem;
    color: #334d66;
    margin: 4px 0 0 0;
    letter-spacing: 1px;
  }

  .card {
    background: #0d1520;
    border: 1px solid #0a2040;
    border-radius: 10px;
    padding: 22px 22px 18px 22px;
    margin-bottom: 14px;
  }
  .card-title {
    font-family: 'Share Tech Mono', monospace;
    font-size: 0.72rem;
    color: #00a8cc;
    letter-spacing: 2px;
    margin-bottom: 14px;
  }

  .terminal {
    background: #060c12;
    border: 1px solid #0a2040;
    border-radius: 8px;
    padding: 18px;
    font-family: 'Share Tech Mono', monospace;
    font-size: 0.85rem;
    color: #00e87a;
    white-space: pre-wrap;
    min-height: 180px;
    line-height: 1.6;
  }

  .log-box {
    background: #08090c;
    border: 1px solid #0a1a2a;
    border-radius: 8px;
    padding: 14px;
    font-family: 'Share Tech Mono', monospace;
    font-size: 0.75rem;
    color: #3a5a7a;
    white-space: pre-wrap;
    min-height: 80px;
    max-height: 180px;
    overflow-y: auto;
  }

  .badge-ready { color: #334455; font-family: monospace; font-size:0.78rem; }
  .badge-ok    { color: #00e87a; font-family: monospace; font-size:0.78rem; }
  .badge-warn  { color: #ddaa00; font-family: monospace; font-size:0.78rem; }

  div[data-testid="stSelectbox"] > div,
  div[data-testid="stTextInput"] > div > div {
    background-color: #0d1520 !important;
    border-color: #1e3050 !important;
    color: #c8d8e8 !important;
  }
  .stButton > button {
    background: #004e92;
    color: #ffffff;
    font-family: 'Share Tech Mono', monospace;
    letter-spacing: 1px;
    border: none;
    border-radius: 8px;
    height: 46px;
    font-size: 0.9rem;
    width: 100%;
  }
  .stButton > button:hover { background: #0066bb; }

  .app-footer {
    font-size: 0.7rem;
    color: #1a2a3a;
    text-align: center;
    margin-top: 2rem;
    font-family: 'Share Tech Mono', monospace;
  }

  #MainMenu, footer, header { visibility: hidden; }
</style>
""", unsafe_allow_html=True)

# -- Session state ------------------------------------------------------------
if "log" not in st.session_state:
    st.session_state.log = []
if "result_text" not in st.session_state:
    st.session_state.result_text = (
        "BIOS Recovery Manager ready.\n\n"
        "Select manufacturer and model,\n"
        "enter the recovery code, then\n"
        "press GENERATE PASSWORD.\n"
    )
if "status" not in st.session_state:
    st.session_state.status = ("ready", "Awaiting input")

# -- Header -------------------------------------------------------------------
st.markdown("""
<div class="app-header">
  <p class="app-title">BIOS RECOVERY MANAGER</p>
  <p class="app-subtitle">
    PERSONAL LAPTOP ASSET MANAGEMENT &nbsp;|&nbsp;
    HP &nbsp;DELL &nbsp;LENOVO &nbsp;FUJITSU &nbsp;SAMSUNG
  </p>
</div>
""", unsafe_allow_html=True)

# -- Two-column layout --------------------------------------------------------
left, right = st.columns([2, 3], gap="medium")

# -- LEFT: Inputs -------------------------------------------------------------
with left:
    st.markdown('<div class="card">', unsafe_allow_html=True)
    st.markdown('<p class="card-title">DEVICE INFORMATION</p>', unsafe_allow_html=True)

    manufacturer = st.selectbox("Manufacturer", options=list(MANUFACTURERS.keys()), key="mfr")
    config       = MANUFACTURERS[manufacturer]
    model        = st.selectbox("Model Series", options=config["models"], key="model")
    recovery_code = st.text_input(
        "Recovery Key / Hash Code",
        placeholder="Code shown on BIOS lock screen",
        key="code"
    )
    st.caption(config["hash_format"])
    notes    = st.text_area("Asset Notes (optional)", height=90, key="notes")
    generate = st.button("GENERATE PASSWORD")
    st.markdown('</div>', unsafe_allow_html=True)

# -- RIGHT: Output ------------------------------------------------------------
with right:
    st.markdown('<div class="card">', unsafe_allow_html=True)
    st.markdown('<p class="card-title">RECOVERY RESULTS</p>', unsafe_allow_html=True)

    status_class = {
        "ready": "badge-ready",
        "ok":    "badge-ok",
        "warn":  "badge-warn"
    }.get(st.session_state.status[0], "badge-ready")

    st.markdown(
        f'<p class="{status_class}">&#9679; &nbsp;{st.session_state.status[1]}</p>',
        unsafe_allow_html=True
    )

    st.markdown(
        f'<div class="terminal">{st.session_state.result_text}</div>',
        unsafe_allow_html=True
    )

    st.code(st.session_state.result_text, language=None)

    st.markdown('<p class="card-title" style="margin-top:18px;">SESSION LOG</p>',
                unsafe_allow_html=True)

    log_content = "\n".join(st.session_state.log) if st.session_state.log else "No entries yet."
    st.markdown(f'<div class="log-box">{log_content}</div>', unsafe_allow_html=True)

    if st.button("Clear Results", key="clear"):
        st.session_state.result_text = (
            "BIOS Recovery Manager ready.\n\n"
            "Select manufacturer and model,\n"
            "enter the recovery code, then\n"
            "press GENERATE PASSWORD.\n"
        )
        st.session_state.status = ("ready", "Awaiting input")
        st.rerun()

    st.markdown('</div>', unsafe_allow_html=True)

# -- Generate logic -----------------------------------------------------------
if generate:
    if not recovery_code.strip():
        st.warning("Please enter the recovery key or hash code shown on the BIOS lock screen.")
    else:
        results = AlgorithmRouter.route(manufacturer, model, recovery_code.strip())

        ts  = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        out = f"Timestamp    : {ts}\n"
        out += f"Manufacturer : {manufacturer}\n"
        out += f"Model Series : {model}\n"
        out += f"Input Code   : {recovery_code.strip()}\n"
        out += "=" * 44 + "\n"
        for algo_name, password in results:
            out += f"\n  [{algo_name}]\n"
            out += f"  Result : {password}\n"
        out += "\n" + "=" * 44

        st.session_state.result_text = out

        short    = recovery_code.strip()[:10] + ("..." if len(recovery_code.strip()) > 10 else "")
        ts_short = datetime.datetime.now().strftime("%H:%M:%S")
        st.session_state.log.append(
            f"[{ts_short}]  {manufacturer:<16}  {model:<14}  {short}"
        )

        flagged = any(
            kw in p.lower()
            for _, p in results
            for kw in ("error", "require", "not implemented", "stub")
        )
        st.session_state.status = (
            ("warn", "Complete - see notes in result") if flagged
            else ("ok",   "Complete - password generated")
        )
        st.rerun()

# -- Footer -------------------------------------------------------------------
st.markdown(
    '<p class="app-footer">'
    'Personal use only &nbsp;|&nbsp; '
    'Based on publicly documented BIOS recovery research &nbsp;|&nbsp; '
    'Dogbert bios-pwgen &nbsp;|&nbsp; bios-pw.org'
    '</p>',
    unsafe_allow_html=True
)
