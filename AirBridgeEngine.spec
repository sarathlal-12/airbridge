# -*- mode: python ; coding: utf-8 -*-
# AirBridge engine: the AirPlay receiver (engine/airplay2/ap2-receiver.py) as one folder.
# Build (from G:\AirBridge, with the engine venv):
#   engine\venv\Scripts\python.exe -m PyInstaller --noconfirm --clean AirBridgeEngine.spec
#   -> dist\engine\AirBridgeEngine.exe
from PyInstaller.utils.hooks import collect_all

datas, binaries, hiddenimports = [], [], []
# av ships FFmpeg DLLs; zeroconf/comtypes/pycaw load submodules dynamically.
for pkg in ('av', 'zeroconf', 'comtypes', 'pycaw', 'Crypto', 'srptools', 'netifaces', 'ifaddr'):
    d, b, h = collect_all(pkg)
    datas += d; binaries += b; hiddenimports += [m for m in h if '.test' not in m]
hiddenimports += ['pyaudio', 'biplist', 'hkdf', 'hexdump', 'audioop']

a = Analysis(
    ['engine/airplay2/ap2-receiver.py'],
    pathex=['engine/airplay2'],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=['tkinter', 'comtypes.test', 'PyInstaller'],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

# Console exe: the tray app reads its output for status and starts it hidden (CreateNoWindow).
exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='AirBridgeEngine',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=True,
)
coll = COLLECT(exe, a.binaries, a.datas, strip=False, upx=False, name='engine')
