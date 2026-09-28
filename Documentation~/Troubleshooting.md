# Troubleshooting

## GameAnalytics duplicate types

Do not install `com.gameanalytics.sdk` while `Assets/GameAnalytics` remains. Remove the legacy import using its documented uninstall procedure before enabling the TTG UPM adapter.

## Firebase duplicate assemblies

Use either the Assets `.unitypackage` workflow or the UPM tarball workflow for every Firebase product. Never mix them. Keep Analytics, Crashlytics, App, and EDM4U on compatible versions.

## Services stay deferred

The host must provide a resolved `TTGConsentState` before consent-dependent SDKs initialize.

## Builds are blocked

Open the setup window and run validation for the active target. Disabled integrations do not block builds; enabled integrations must have their active-platform files, keys, and IDs.
