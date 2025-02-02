<p align="center">
  <img src="https://carbonmod.gg/assets/media/carbonlogo_w.png" />
</p><p align="center">
  <a href="https://github.com/CarbonCommunity/Carbon/releases/tag/edge_build"><img src="https://github.com/CarbonCommunity/Carbon/actions/workflows/edge-build.yml/badge.svg" /></a>
  <a href="https://github.com/CarbonCommunity/Carbon/releases/tag/preview_build"><img src="https://github.com/CarbonCommunity/Carbon/actions/workflows/preview-build.yml/badge.svg" /></a>
  <a href="https://github.com/CarbonCommunity/Carbon/releases/latest"><img src="https://github.com/CarbonCommunity/Carbon/actions/workflows/production-build.yml/badge.svg" /></a>
  <br />
    <a href="https://github.com/CarbonCommunity/Carbon/releases/tag/rustbeta_staging_build"><img src="https://github.com/CarbonCommunity/Carbon/actions/workflows/rust-staging-build.yml/badge.svg" /></a>
    <a href="https://github.com/CarbonCommunity/Carbon/releases/tag/rustbeta_release_build"><img src="https://github.com/CarbonCommunity/Carbon/actions/workflows/rust-release-build.yml/badge.svg" /></a>
    <a href="https://github.com/CarbonCommunity/Carbon/releases/tag/rustbeta_aux01_build"><img src="https://github.com/CarbonCommunity/Carbon/actions/workflows/rust-aux01-build.yml/badge.svg" /></a>
    <a href="https://github.com/CarbonCommunity/Carbon/releases/tag/rustbeta_aux02_build"><img src="https://github.com/CarbonCommunity/Carbon/actions/workflows/rust-aux02-build.yml/badge.svg" /></a>
  <br />
  <a href="https://github.com/CarbonCommunity/Carbon/blob/develop/LICENSE"><img alt="GitHub" src="https://img.shields.io/github/license/CarbonCommunity/Carbon" /></a>
  <a href="https://www.nuget.org/packages/Carbon.Community"><img alt="NuGet" src="https://img.shields.io/nuget/v/Carbon.Community.svg" /></a>
  <a href="https://github.com/GameServerManagers/LinuxGSM/releases/latest"><img src="https://img.shields.io/badge/LinuxGSM-v23.2.0-informational" /></a>
  <hr />
</p>

Carbon is a self-updating, lightweight, intelligent mod loader for Rust utilizing the latest C# and Harmony for the best performance and stability possible. Its robust framework and backward compatibility with Oxide plugins make it the ultimate replacement for those wanting better functionality and performance from their plugins!

Carbon has all the creature comforts you need to run your server, such as a permission system, user system, and so much more. Carbon is developed by experienced developers and server owners working to take the tedium out of hosting servers and make configuration and setup seamless with an integrated GUI in-game to manage everything!

## :package: Download
Start using Carbon today, download the latest version from our [releases page][production].
We also provide a [quick start script][quick-start] to get your server running in minutes, available for Windows and Linux.

## :blue_book: Documentation
For more in-depth Carbon documentation, from builds and deployment, check [here][documentation].
Find all currently available hooks [here][hooks].
If you are a developer take a look at our [Wiki page][wiki].

## :question: Support
Join our official [Discord server][discord] for support, more frequent development info, discussions and future plans.

## ⚙️ Development
To help us and work with us on the project, or get started with Carbon's structure, follow this for understanding how it works.
The project is split in various essential and mostly independent [components](https://github.com/CarbonCommunity/Carbon/tree/develop/Carbon.Core/Carbon.Components), making it easier to organise and follow.

### Getting Started
Upon cloning Carbon, all you need to execute is [`setup.bat`](https://github.com/CarbonCommunity/Carbon/blob/develop/setup.bat) to initialize the entire project and its dependencies.

### Branches
The following branches are shared across all Component and Hook projects and synchronized accordingly with the main project ([this one](https://github.com/CarbonCommunity/Carbon)) and its branches.
- `develop` branch is the primary base branch of Carbon. It's used to be merged into Rust beta branches as well as the `production` (live) branch.
  - Used against Rust `public` branch.
  - Public [build](https://github.com/CarbonCommunity/Carbon/releases/tag/edge_build), gets updated every time we commit changes to Carbon.
- `preview` branch is usually synced up with `develop` and has experimental features that may or may not be brought into `develop`.
  - Used against Rust `public` branch.
  - Public [build](https://github.com/CarbonCommunity/Carbon/releases/tag/preview_build), gets manually triggered to get updated, not very often.
- `production` branch is the primary (live) branch of Carbon and [main release](https://github.com/CarbonCommunity/Carbon/releases/tag/production_build) is built off of.
  - Used against Rust `public` branch or `release` on Rust wipe day.
  - Public [build](https://github.com/CarbonCommunity/Carbon/releases/tag/production_build), gets updated twice times a month, excluding important hotfix patches.
- `rust_beta/staging` is often times synced up with `develop` and has changes that might come to Rust `release|public` branch in a future update, which can contain mandatory changes to Carbon to address the Rust changes.
  - Used against Rust `staging` branch.
  - Public [build](https://github.com/CarbonCommunity/Carbon/releases/tag/rustbeta_staging_build), gets updated a few times a month. Usually only hooks get updated. Mainly merged from `develop`.
- `rust_beta/aux01` is often times synced up with `rust_beta/staging` and is minimally supported, just enough to be able to run Carbon on AUX01. This Rust branch is extremely unstable and contains things that may or may not ever be merged to the main version of Rust.
  - Used against Rust `aux01` branch. The staging branch beta client is not always available.
  - Public [build](https://github.com/CarbonCommunity/Carbon/releases/tag/rustbeta_aux01_build), gets updated a few times a month. Usually only hooks get updated.
- `rust_beta/aux02` is often times synced up with `rust_beta/staging` and is minimally supported, just enough to be able to run Carbon on AUX02. This Rust branch is extremely unstable and contains things that may or may not ever be merged to the main version of Rust.
  - Used against Rust `aux02` branch. The staging branch beta client is not always available.
  - Public [build](https://github.com/CarbonCommunity/Carbon/releases/tag/rustbeta_aux02_build), gets updated a few times a month. Usually only hooks get updated.

### Components
- [Carbon.Preloader](https://github.com/CarbonCommunity/Carbon.Preloader/tree/develop): Runtime preloader of dependencies and responsible for the self-updating process. It invokes Carbon.Startup.
- [Carbon.Startup](https://github.com/CarbonCommunity/Carbon.Startup/tree/main): Handles in-memory Rust assembly patching and publicizing and exporting if Developer Mode is enabled in the config.
- [Carbon.Bootstrap](https://github.com/CarbonCommunity/Carbon.Bootstrap/tree/develop): Initial Carbon execution and boot in the primary app-domain.
- [Carbon.Common](https://github.com/CarbonCommunity/Carbon.Common/tree/develop): The very basis of Carbon, tools and extensions for overall use and functionality. Primarily a center piece for all dependant sub-components.
- [Carbon.SDK](https://github.com/CarbonCommunity/Carbon.SDK/tree/develop): Infrastructural and contractual features with no implementation. An easy way to identify and organise the structure of our systems.
- [Carbon.Modules](https://github.com/CarbonCommunity/Carbon.Modules/tree/develop): Carbon optional modules expanding functionality, enhanced QoL and tools.
- [Carbon.Compat](https://github.com/CarbonCommunity/Carbon.Compat/tree/develop): Previously known as Carbon Compatibility Loader written by Patrette (community member).
- [Carbon.Test](https://github.com/CarbonCommunity/Carbon.Test/tree/main): Integral implementation for automated testing rules and events.

### Hooks
Carbon's hooks are managed in a separate location:
- [Carbon.Hooks.Base](https://github.com/CarbonCommunity/Carbon.Hooks.Base/tree/develop): Includes ground level dynamic and static patching instructions supplementing necessary events for Carbon's own runtime.
- [Carbon.Hooks.Community](https://github.com/CarbonCommunity/Carbon.Hooks.Community/tree/develop): Community curated patches and hooks.
- [Carbon.Hooks.Oxide](https://github.com/CarbonCommunity/Carbon.Hooks.Oxide/tree/develop): Oxide compatibility package, primarily utilized for internal use whenever going through the automatic patch code generation process.

### Dynamic Hooks
Each Carbon version has its own protocol which relative to that, so does the hooks that get automatically updated upon boot.
You can find the directory [here](https://carbonmod.gg/redist/server).

### Building
To locally build Carbon from scratch, execute the [`build.bat`](https://github.com/CarbonCommunity/Carbon/tree/develop/Tools/Build/win) file, and find the results in the root of Carbon, under `./Release`.

## :heart: Sponsor

If you would like to [sponsor][patreon] the project the best way is to use [Patreon].

We would like to thank everyone who sponsors us.

[hooks]: https://docs.carbonmod.gg/docs/core/hooks
[discord]: https://discord.gg/carbonmod
[documentation]: https://docs.carbonmod.gg/
[patreon]: https://patreon.com/CarbonCommunity
[production]: https://github.com/CarbonCommunity/Carbon.Core/releases/tag/production_build
[quick-start]: https://github.com/CarbonCommunity/Carbon.QuickStart
