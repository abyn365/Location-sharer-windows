# Installer scaffold

This folder contains a WiX MSI scaffold plus a scheduled-task step that runs only during installation when the installer property is enabled.

Before shipping:
- replace `YourCompany`
- add a real icon
- wire in your code-signing certificate in the release pipeline
- test the optional scheduled-task path on a clean Windows VM
