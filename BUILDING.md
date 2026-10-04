# Building VALKEA

Needs the .NET 8 SDK, Valheim with BepInEx, and Jotunn 2.30.2 or newer.

1. Put the `bjorksnas` asset bundle at `assets/bjorksnas`. It is kept out of git: at 146 MB it is over GitHub's
   100 MB file limit.

2. Build:

   ```
   dotnet build VALKEA.csproj -c Release
   ```

   The DLL lands in `dist/VALKEA.dll`. The project looks for the game in the default Steam folder and for Jotunn in
   the r2modman profile `LocalTesting`; override with `-p:GamePath=...` and `-p:JotunnPath=...\Jotunn.dll`.

3. Package for Thunderstore:

   ```
   dotnet build VALKEA.csproj -c Release -t:PackageMod
   ```

   writes `dist/VALKEA-<version>.zip` with the DLL, `package/manifest.json`, `package/icon.png`,
   `package/LICENSE.txt`, `README.md` and `PrefabList.md`. Keep the version in `Plugin.cs`, `VALKEA.csproj` and
   `package/manifest.json` the same.
