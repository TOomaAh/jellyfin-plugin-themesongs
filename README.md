<h1 align="center">Jellyfin Theme Songs Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org">Jellyfin Project</a></h3>

<p align="center">
  <a href="https://github.com/your-username/jellyfin-plugin-themesongs/actions/workflows/ci.yml">
    <img src="https://github.com/your-username/jellyfin-plugin-themesongs/workflows/Build%20Plugin/badge.svg" alt="Build Status">
  </a>
  <a href="https://github.com/your-username/jellyfin-plugin-themesongs/actions/workflows/tests.yml">
    <img src="https://github.com/your-username/jellyfin-plugin-themesongs/workflows/Tests%20%26%20Quality/badge.svg" alt="Tests">
  </a>
  <a href="https://codecov.io/gh/your-username/jellyfin-plugin-themesongs">
    <img src="https://codecov.io/gh/your-username/jellyfin-plugin-themesongs/branch/master/graph/badge.svg" alt="Coverage">
  </a>
  <a href="https://github.com/your-username/jellyfin-plugin-themesongs/releases">
    <img src="https://img.shields.io/github/v/release/your-username/jellyfin-plugin-themesongs" alt="Release">
  </a>
  <a href="https://github.com/your-username/jellyfin-plugin-themesongs/blob/master/LICENSE">
    <img src="https://img.shields.io/github/license/your-username/jellyfin-plugin-themesongs" alt="License">
  </a>
</p>

<p align="center">
Jellyfin Theme Songs plugin automatically downloads theme songs for your TV show library with configurable providers and audio normalization.
</p>

## ✨ Features

- 🎵 **Automatic Theme Song Downloads**: Seamlessly downloads theme songs for your TV shows
- 🔧 **Configurable Providers**: Choose between Plex and TelevisionTunes providers with custom priorities
- 🎚️ **Audio Normalization**: Optional FFmpeg-based audio normalization with configurable volume levels
- 📅 **Scheduled Tasks**: Automatic background downloads with customizable scheduling
- 🛠️ **RESTful API**: Programmatic control via HTTP endpoints
- 🏗️ **Modern Architecture**: Built with .NET 8, dependency injection, and async/await patterns

## 📋 Requirements

- Jellyfin 10.10.0 or higher
- .NET 8.0 runtime
- FFmpeg (optional, for audio normalization)
- TVDb provider enabled in Jellyfin

## 🚀 Installation

### From GitHub Releases (Recommended)
1. Download the latest `jellyfin-plugin-themesongs-x.x.x.zip` from [Releases](https://github.com/your-username/jellyfin-plugin-themesongs/releases)
2. Extract the ZIP file
3. Copy the contents to your Jellyfin plugins directory:
   - **Windows**: `%ProgramData%\Jellyfin\Server\plugins\Theme Songs\`
   - **Linux**: `/var/lib/jellyfin/plugins/Theme Songs/`
   - **Docker**: `/config/plugins/Theme Songs/`
4. Restart Jellyfin
5. Configure the plugin in Dashboard → Plugins → Theme Songs

### From Repository
1. In Jellyfin, go to Dashboard → Plugins → Repositories
2. Add repository: `https://raw.githubusercontent.com/your-username/jellyfin-plugin-themesongs/master/manifest.json`
3. Go to Catalog and search for "Theme Songs"
4. Install and restart Jellyfin

## ⚙️ Configuration

### Provider Settings
- **Enable/Disable Providers**: Toggle Plex and TelevisionTunes providers
- **Provider Priorities**: Set search order (1 = highest priority)
- **Default Configuration**: Plex (Priority 1), TelevisionTunes (Priority 2)

### Audio Settings
- **Normalize Audio**: Enable/disable audio normalization
- **Target Volume**: Configure normalization level (-30dB to 0dB, default: -15dB)

### Usage Options
1. **Manual Download**: Use the "Download Theme Songs" button in plugin configuration
2. **Scheduled Task**: Configure automatic downloads in Dashboard → Scheduled Tasks
3. **API Endpoint**: `POST /ThemeSongs/DownloadTVShows` for programmatic access

## 🎯 How It Works

1. **Library Scan**: Plugin scans your TV show library for series without theme songs
2. **Provider Search**: Searches configured providers in priority order using TVDb IDs
3. **Download & Process**: Downloads found theme songs to temporary cache
4. **Audio Processing**: Optionally normalizes audio using FFmpeg
5. **Final Placement**: Moves processed files to series directories as `theme.mp3`





## Build Process
1. Clone or download this repository
2. Ensure you have .NET Core SDK setup and installed
3. Build plugin with following command.
```sh
dotnet publish --configuration Release --output bin
```
4. Place the resulting .dll file in a folder called ```plugins/Merge versions``` under  the program data directory or inside the portable install directory


