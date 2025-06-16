# WindowGopher

**WindowGopher** is a lightweight Windows WPF app that helps single-monitor users automate switching between two application windows, ideal for multitasking without constant Alt-Tabbing.

By default, it brings a selected window to the foreground every **23 seconds** and returns focus to another active window after **3 seconds**. These time intervals are fully customizable through the user interface.

![User interface](interface.png)

## Use Case

- Stay focused on one task (e.g., coding, writing), while periodically checking another window (e.g., a chat, logs, monitoring app).
- Avoid manually switching windows while juggling two ongoing tasks.

## Features

- Select any open window from a list of active application windows.
- Automatically switch focus to the selected window at a regular interval.
- Return control to the previously active window after a short duration.
- Customize the time intervals to suit your workflow.
- Simple and clean WPF GUI.

## How It Works

- Uses Windows API calls (`user32.dll`) to manage window focus.
- Implements safeguards to work around common Windows restrictions when switching window focus programmatically.
- Written entirely in C# with WPF and .NET 9.

## Requirements

- Windows 10 or higher

## Known Limitations

- Switching process will continue for one more cycle after the app has ended.
- The app will continue to run even if one of the targeted windows has been closed.

## License

MIT License. See LICENSE.md for details.