# Third-party notices

AirBridge bundles the following. Each keeps its own license.

## AirPlay engine
- **airplay2-receiver** by the openairplay contributors, https://github.com/openairplay/airplay2-receiver.
  AirBridge ships a modified copy in `engine/airplay2` (playback timing, realtime latency, WASAPI output,
  volume, packaging fixes). The upstream repository does not state a license; all rights in the
  original code remain with its authors.
- **fairplay3.py** (part of the engine): Python implementation by systemcrash, based on the OmgHax C
  implementation. Marked GPLv2 by its author. Its full source is included in this repository.

## Python libraries (inside AirBridgeEngine.exe)
| Library | License |
|---|---|
| PyAV (av) | BSD-3-Clause |
| FFmpeg (bundled with PyAV) | LGPL-2.1-or-later |
| PyAudio | MIT |
| PortAudio (bundled with PyAudio) | MIT |
| python-zeroconf | LGPL-2.1-or-later |
| ifaddr | MIT |
| netifaces | MIT |
| pycryptodome | BSD-2-Clause / Public Domain |
| cryptography | Apache-2.0 / BSD-3-Clause |
| srptools | BSD-3-Clause |
| hkdf | BSD-2-Clause |
| biplist | BSD-3-Clause |
| hexdump | Public Domain |
| comtypes | MIT |
| pycaw | MIT |
| requests, urllib3, idna, charset-normalizer, certifi | Apache-2.0 / MIT / BSD / MPL-2.0 |
| Python 3.11 runtime | PSF License |

## .NET
| Library | License |
|---|---|
| .NET 8 runtime | MIT |
| Serilog, Serilog.Sinks.File | Apache-2.0 |

AirPlay and iPhone are trademarks of Apple Inc. AirBridge is not affiliated with or endorsed by Apple.
