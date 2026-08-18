# Vercidium Audio

Raytraced audio addon with realistic muffling, reverb, ambience and visualisation for Mono Godot 4, using OpenAL Soft as the audio backend.

> [!WARNING]
> This plugin is experimental and requires testing and feedback

This repository requires Vercidium Audio v1.7.0 and OpenAL Soft to run:
- Download the Vercidium Audio SDK from [vercidium.com](https://vercidium.com)
- Download the OpenAL Soft DLL from [github.com/kcat/openal-soft](https://github.com/kcat/openal-soft/releases/tag/1.25.2)

> Please note that the Vercidium Audio SDK is not free for commercial use. See [vercidium.com/eula](https://vercidium.com/eula)

## Features

- Muffle sounds in real time
- Accurate reverb in any environment
- Innovative event-based raytracing system
- Realistic energy-based model using materials
- Dynamic scene updates - automatically handles moving objects

## References
- [godot-openal repo](https://github.com/vercidium-audio/godot-openal)
- [Vercidum Audio documentation](https://vercidium.com/docs)

## Requirements

- This plugin depends on the [godot-openal](https://github.com/vercidium-audio/godot-openal) plugin. If you wish to use other audio middleware instead, use the raw [vaudio-godot](https://github.com/vercidium-audio/vaudio-godot) plugin
- Godot 4.x with C# support
- [Vercidium Audio SDK](https://vercidium.com)

## Installation

Setup instructions are [available here](https://vercidium.com/docs/godot/getting-started).

Copy `vaudio.dll` from the Vercidium Audio SDK into `addons/vaudio-godot-openal/bin/` (this folder is git-ignored). The plugin will automatically reference it from your `.csproj`.

## Visual Studio

To run your Godot project from Visual Studio, click the small dropdown arrow next to `your_game` and click `your_game Debug Properties`.

Create a new launch profile by clicking the green icon in the top left, and rename it to `Godot`. Then set:
- the executable path
- command line parameters to `--path, --verbose`
- working directory to `.`

![Godot debug properties in visual studio](docs/godot_visual_studio.png)

Then close the window, click the same small dropdown arrow, and select `Godot`. Use this launch profile from now on.

## Licencing

The Vercidium Audio SDK is free for non-commercial products only. To purchase a licence for commercial use, head over to the [Vercidium Audio website](https://vercidium.com).
