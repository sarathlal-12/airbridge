# AirBridge

Play your phone's audio through your Windows PC's speakers or headphones.

- **iPhone** sends audio over **AirPlay** (Wi-Fi). AirBridge shows up in the iPhone's AirPlay list like a speaker.
- **Android** (and iPhone too) sends audio over **Bluetooth**. AirBridge turns your PC into a Bluetooth speaker.

It runs quietly in the system tray.

## Download

Get **AirBridge_Setup_vX.Y.Z.exe** from [Releases](https://github.com/sarathlal-12/airbridge/releases/latest) and run it.

Needs Windows 10 (version 2004) or Windows 11, 64-bit. The installer asks for admin once, to allow AirPlay through Windows Firewall.

## Using it

### iPhone (AirPlay)
1. The phone and PC must be on the same Wi-Fi. It works on home Wi-Fi without internet and on a phone hotspot too.
2. Play something, open Control Center, tap the AirPlay icon on the music card and choose **AirBridge**.
3. The iPhone's volume buttons control AirBridge's volume. Windows' volume sets the maximum.

Music, podcasts, YouTube, Instagram, TikTok, Netflix and most games come through. Calls, ringtones, alarms and notification sounds stay on the phone (that's iOS).

### Android (Bluetooth)
1. Pair the phone with Windows: tray icon > **Android / Bluetooth phones** > **Pair a new phone...**
2. In the same menu choose your phone > **Let this phone play here**.
3. Play on the phone. Next time it connects by itself while AirBridge is running. If not, use **Connect now**.

Bluetooth has a little more delay than AirPlay.

## Troubleshooting
- **iPhone doesn't list AirBridge:** check both devices are on the same network. Some routers' "client isolation" or guest networks block AirPlay. If you switched Wi-Fi, give AirBridge a few seconds to move over.
- **On public Wi-Fi** (cafés, hotels) other people on the network can see AirBridge too. Quit it from the tray icon when you don't need it.
- **Logs:** tray icon > **Open logs folder**.

## Building from source

Needs .NET 8 SDK, Python 3.11 and Inno Setup 6 or 7.

```powershell
py -3.11 -m venv engine\venv
engine\venv\Scripts\python.exe -m pip install -r engine\requirements.txt
.\build.ps1        # -> Output\AirBridge_Setup_v<version>.exe
```

For development, `dotnet build` and run `bin\Debug\...\AirBridge.exe`. It uses the Python engine in `engine\venv` directly and logs to `logs\`.

### How it works
- `AirBridge.exe` (C#, .NET 8, WinForms): tray icon, starts and watches the engine, Bluetooth receiving through Windows' `AudioPlaybackConnection` API.
- `engine\AirBridgeEngine.exe`: the AirPlay 2 receiver (Python, built with PyInstaller), based on [openairplay/airplay2-receiver](https://github.com/openairplay/airplay2-receiver) with fixes for clean playback, low latency on video apps, WASAPI output and phone volume control.

## License
AirBridge's own code is MIT (see [LICENSE](LICENSE)). The bundled AirPlay engine and libraries have their own terms; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

AirPlay and iPhone are trademarks of Apple Inc. AirBridge is not affiliated with or endorsed by Apple.
