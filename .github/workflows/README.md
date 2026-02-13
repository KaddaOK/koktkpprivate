# GitHub Actions CI/CD for KOKT Karaoke Party

This repository includes automated GitHub Actions workflows for building, testing, and releasing the KOKT Karaoke Party Godot C# project.

## Workflows

### 1. Build and Test (`build.yml`)

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches

**What it does:**
- Runs unit tests using the .NET test runner
- Builds the project for all platforms
- Uploads build artifacts that can be downloaded from the Actions tab

### 2. Release (`release.yml`)

**Triggers:**
- Push of version tags (format: `v*.*.*`, e.g., `v1.0.0`, `v2.1.3`)

**What it does:**
- Builds the project for all platforms
- Creates compressed archives for each platform
- Creates a GitHub Release with all platform builds attached
- Automatically generates release notes

## Usage

### Running Tests and Builds

Simply push code to any of the monitored branches (`main`, `develop`) or create a pull request. The build workflow will automatically:

1. Run all unit tests
2. Build for all platforms if tests pass
3. Upload artifacts for download

### Creating Releases

To create a new release:

1. Ensure your code is ready for release
2. Create and push a version tag:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
3. The release workflow will automatically:
   - Build all platforms
   - Create compressed archives
   - Create a GitHub Release
   - Upload all build files to the release

### Downloading Builds

**From Pull Requests/Builds:**
1. Go to the **Actions** tab in your GitHub repository
2. Click on the workflow run you're interested in
3. Scroll down to the **Artifacts** section
4. Download the platform-specific build you need

**From Releases:**
1. Go to the **Releases** section of your repository
2. Click on the release you want
3. Download the appropriate file for your platform from the **Assets** section

## Platform-Specific Notes

### Windows Desktop
- Builds as a `.exe` file
- Packaged as a ZIP archive
- Requires .NET 8.0 Runtime on target machine

## Troubleshooting

### Common Issues

**Build fails with "Export preset not found":**
- Make sure you have created export presets in Godot with the exact names specified in the workflow
- Ensure the presets are saved in your project's `export_presets.cfg` file

**Tests fail:**
- Check the test output in the Actions tab
- Ensure all dependencies are properly restored
- Verify test projects build correctly locally

### Customization

To modify the workflows:

1. **Change target branches:** Edit the `on.push.branches` and `on.pull_request.branches` sections
2. **Add/remove platforms:** Modify the `matrix` section in the build jobs
3. **Change Godot version:** Update the `GODOT_VERSION` environment variable
4. **Modify export preset names:** Update the `export-preset` values in the matrix

## Dependencies

The workflows automatically handle:
- .NET 8.0 SDK installation
- Godot 4.4.1 installation and setup
- NuGet package restoration
- Export template downloads

No manual setup is required on the GitHub Actions runners.