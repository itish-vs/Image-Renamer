# Image Renamer

A small Windows desktop utility built with WPF on .NET 10. It renames image files in one selected folder to a sequence (`1.jpg`, `2.png`, `3.gif`, …) while keeping each original extension unchanged.

## Features

- Dark, Material-inspired interface with a custom application icon.
- Shows the number of image files found when the app opens and whenever the folder is rescanned.
- Reads its folder path and extension list from `appsettings.json` beside the executable.
- The Browse button updates the folder path and saves it back to `appsettings.json`.
- Uses background tasks and progress callbacks so scanning and renaming do not freeze the UI.
- Starts renaming immediately when Rename images is clicked, without an extra confirmation prompt.
- Shows a Process complete alert after a successful rename, then resets progress and status and refreshes the image count.
- Uses a two-phase rename. Files are first moved into a uniquely named temporary directory inside the selected folder. Only after all source files are staged are they moved back as `1`, `2`, `3`, and so on. This handles existing names such as `10.jpg` safely.
- Does not overwrite existing files. If a move fails, the app attempts to restore the original file names.
- Does not read or modify image contents. It only changes file names.

## Configure the folder

Open `appsettings.json` beside `ImageRenamer.exe` and edit `ImageFolder`:

```json
{
  "ImageRenamer": {
    "ImageFolder": "D:\\Photos\\ToRename",
    "TempFolderPrefix": ".image-renamer-tmp",
    "SupportedExtensions": [
      ".jpg", ".jpeg", ".png", ".bmp", ".gif",
      ".tif", ".tiff", ".webp", ".ico", ".heic",
      ".heif", ".avif", ".svg", ".dng", ".cr2", ".cr3",
      ".nef", ".nrw", ".arw", ".orf", ".rw2", ".raf",
      ".pef", ".srw", ".tga", ".psd"
    ]
  }
}
```

The default configuration is in the shipped `appsettings.json`. Extensions are matched case-insensitively. This is an extension based filter, not an image decoder; add any additional image formats used in your workflow to the list. Add or remove extensions in `SupportedExtensions` to control which files are treated as images. Only files directly inside `ImageFolder` are included; subfolders are not modified.

The Browse button can also select a folder and save the new path to `appsettings.json`. Keep the published app in a folder where the current Windows user has permission to update that JSON file. If the folder is read-only, the selected path works for the current session but cannot be persisted.

## Publish a Windows executable

Requirements: Windows 10 or Windows 11, and the .NET 10 SDK.

1. Extract the project folder.
2. Open PowerShell in the `ImageRenamer` project folder.
3. Run the publish script:

```powershell
.\Publish-Win-x64.ps1
```

The script clears old publish output, publishes a self-contained single-file executable, keeps English satellite resources only, and validates the final folder.

Expected output in `ImageRenamer\publish\`:

```text
ImageRenamer.exe
appsettings.json
```

**Important:** Use the `publish` folder. Do not copy files from `bin\Release\net10.0-windows\win-x64\`, which is an intermediate build folder and contains many individual runtime files. The published executable is self-contained and does not require a separately installed .NET runtime. Keep `appsettings.json` beside `ImageRenamer.exe`.

To publish for ARM64, change `win-x64` to `win-arm64` on an ARM64 Windows build machine.

## Rename behavior and safety

- Files are ordered by their current file name, case-insensitively, with an ordinal tie-breaker.
- The sequence starts at `1` and is continuous across all configured extensions.
- Original extension text is preserved. For example, `holiday.JPG` becomes `1.JPG`.
- Existing numbered names do not cause an error because all selected images are moved out of the source folder before new names are assigned.
- Non-image files and subfolders are untouched.
- Do not modify, move, or open files in the selected folder while a rename is running.
- Back up important files before a bulk rename. If Windows is forcibly shut down or the app process is terminated during the operation, a temporary directory with the configured prefix may remain. Do not delete such a directory until its contents have been checked.

## Project structure

```text
ImageRenamer/
  Assets/                 App icon source and ICO
  Models/                 Settings model
  Services/                Settings and rename logic
  App.xaml                 Theme resources
  MainWindow.xaml          Main UI
  appsettings.json         Runtime configuration
  ImageRenamer.csproj      .NET 10 WPF project
```

The UI has no third-party NuGet dependencies. The dark theme and controls are implemented with WPF styles, keeping the published app straightforward to deploy.
