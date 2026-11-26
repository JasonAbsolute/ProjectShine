# libusb for the GameCube adapter (SMS 265)

`SmsGcAdapter` (assets/Sms/Input/SmsGcAdapter.cs) reads the GameCube controller adapter (Nintendo's Wii U one, the Mayflash in its Wii U / Switch
mode; USB 057E:0337) straight over USB. On Windows it goes through the WinUSB driver (253: Zadig). On macOS and Linux it goes through **libusb-1.0**,
the way Dolphin does -- macOS attaches no driver of its own to the adapter, so there is nothing to install and no Zadig step: plug it in, switch on
"Wii U" / "Switch", play.

## What is here

- `macos/libusb-1.0.0.dylib` -- libusb 1.0.30, Homebrew's arm64 build (the `libusb` bottle), its install name set to `@rpath/libusb-1.0.0.dylib` and
  re-signed ad hoc. Apple silicon only (Homebrew ships no Intel bottle any more); an Intel Mac needs its own copy (see "another copy" below).
- `macos/COPYING` -- libusb's licence, the GNU LGPL 2.1. The game only loads the library at run time (dlopen), which the LGPL allows without any
  change to the game's own licence; the licence text ships with the library.

## How the game finds it

The reader looks, in this order, and takes the first that loads (the path it took is `SmsGcAdapter.LibUsbPath`; `LastError` says when none did):

1. `$SMS_LIBUSB` -- a full path to a libusb-1.0 library, for trying another copy.
2. Next to the game's executable, and in `../Frameworks/` -- an exported `.app`: `Contents/MacOS/libusb-1.0.0.dylib` or `Contents/Frameworks/`.
3. The project's own copy, `res://addons/libusb/macos/libusb-1.0.0.dylib` -- running from the editor or with `--path`.
4. Homebrew's: `/opt/homebrew/opt/libusb/lib/`, `/opt/homebrew/lib/`, `/usr/local/opt/libusb/lib/`, `/usr/local/lib/` (`brew install libusb`).
5. The system's default library search (`libusb-1.0.0.dylib` / `libusb-1.0.so.0`).

Linux: the distro's `libusb-1.0.so.0` (step 5) and Dolphin's udev rule for the adapter (`/etc/udev/rules.d/51-gcadapter.rules`:
`SUBSYSTEM=="usb", ENV{DEVTYPE}=="usb_device", ATTRS{idVendor}=="057e", ATTRS{idProduct}=="0337", MODE="0666"`), then replug.

## Exporting a Mac build

Godot does not copy a `.dylib` out of the project into the `.app`. After exporting, copy `macos/libusb-1.0.0.dylib` into the app's
`Contents/MacOS/` (next to the executable), or `Contents/Frameworks/`; if the app is signed, sign the dylib with the same identity.
Without it the game still runs -- the adapter reader says so once in the log (`LibUsbHint`) and stays idle, and the Mayflash still works in its
PC mode as an ordinary pad (no separate L / R clicks: the R click then comes from a full pull, Mario.SmsRClick).
