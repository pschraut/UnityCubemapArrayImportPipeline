# Changelog
All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.4.0] - 2022-03-11
After installing this update, it will trigger a reimport of all CubemapArray assets in the project and CubemapArray's will no longer be readable via scripts by default.
### Added
 - Added ability to toggle whether the CubemapArray is readable from scripts at the expense of consuming more memory when turned on. The default is off.


## [1.3.0] - 2021-03-06
### Fixed 
 - Fixed CubemapArray asset not updating its texture format when changing the build target with [Asset Import Pipeline V2](https://blogs.unity3d.com/2019/10/31/the-new-asset-import-pipeline-solid-foundation-for-speeding-up-asset-imports/) being used. Thanks to Bastien for the help (actually providing the fix/workaround).


## [1.2.0] - 2020-11-03
### Fixed 
 - Fixed compile error in Unity 2020.2 (ScriptedImporter was moved to a different namespace)
 - Don't display the CubemapArray imported object twice in the Inspector


## [1.1.0] - 2020-05-18
### Fixed 
 - When running in Unity 2020.1 and newer, use the new built-in CubemapArray preview when viewing a CubemapArray asset in the Inspector. The built-in preview is way nicer and has more features than my implementation for Unity 2019.3.
 - Several minor documentation fixes.
 
 
## [1.0.0] - 2019-11-03
 - First release