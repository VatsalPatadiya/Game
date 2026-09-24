# GooglePackages (not committed to git)

This directory holds the vendored UPM tarballs that `Packages/manifest.json`
references via `file:../GooglePackages/...` paths:

- `com.google.external-dependency-manager-1.2.186.tgz`
- `com.google.firebase.app-13.17.0.tgz`
- `com.google.firebase.analytics-13.17.0.tgz`

They're excluded from git (see `.gitignore`) because they're ~60MB of
third-party binaries that are trivially re-downloadable and add no history
value. On a fresh clone, Unity will fail to resolve packages until this
folder is repopulated with the same three files at the same versions.

To regenerate: download the External Dependency Manager for Unity (EDM4U)
release and the Firebase Unity SDK for the matching versions above from
Google's official distribution channels (EDM4U's GitHub releases page, and
the Firebase Unity SDK download page), then place the corresponding `.tgz`
UPM packages in this folder using the exact filenames listed above. If in
doubt about exact source URLs, ask whoever last set up analytics on this
project.
