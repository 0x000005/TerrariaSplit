# TerrariaSplit application updates

TerrariaSplit checks for updates only when the user selects **Settings > About > Check for updates**.
The client reads the latest stable GitHub Release from `0x000005/TerrariaSplit`; drafts and prereleases are not update channels.

## Release contract

1. Set `Version` and `FileVersion` in `TerrariaSplit.WinForms.csproj` to the same four-part value, such as `1.8.2.0`.
2. Publish the WinForms project in Release configuration. The portable target creates `TerrariaSplit-v1.8.2.0-win-x64.zip`.
3. Create a non-draft, non-prerelease GitHub Release tagged `v1.8.2.0` and attach that ZIP without renaming it.
4. Confirm GitHub reports a `sha256:` digest for the uploaded asset. The client refuses assets without that digest.

The archive includes `Runtime/terrariasplit-update-manifest.json`. Its managed roots are replaced transactionally during an update. User-owned `Settings`, `Data`, `Worlds`, and `terrariasplit.log` paths are never managed by the updater.

The first release containing the updater establishes this contract. Older underscore-named archives such as `TerrariaSplit_v1.8.0.3.zip` are intentionally unsupported.
