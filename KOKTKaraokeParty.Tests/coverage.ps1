dotnet build

# generate coverage for the GoDotTest tests
coverlet `
  "./.godot/mono/temp/bin/Debug" --verbosity detailed `
  --target $env:GODOT `
  --targetargs "--run-tests --coverage --quit-on-finish" `
  --format "opencover" `
  --output "./coverage/coverage.godot.xml" `
  --exclude-by-file "**/test/**/*.cs" `
  --exclude-by-file "**/*Microsoft.NET.Test.Sdk.Program.cs" `
  --exclude-by-file "**/Godot.SourceGenerators/**/*.cs" `
  --exclude-by-file "**/Chickensoft.*/**/*.cs" `
  --exclude-by-file "**/*.g.cs" `
  --skipautoprops

# generate coverage for the plain XUnit tests
  coverlet `
  "./.godot/mono/temp/bin/Debug/KOKTKaraokeParty.Tests.dll" --verbosity detailed `
  --target "dotnet" `
  --targetargs "test --no-build" `
  --format "opencover" `
  --output "./coverage/coverage.xunit.xml" `
  --exclude-by-file "**/test/**/*.cs" `
  --exclude-by-file "**/*Microsoft.NET.Test.Sdk.Program.cs" `
  --exclude-by-file "**/Godot.SourceGenerators/**/*.cs" `
  --exclude-by-file "**/Chickensoft.*/**/*.cs" `
  --exclude-by-file "**/*.g.cs" `
  --skipautoprops

# Projects included via <ProjectReference> will be collected in code coverage.
# If you want to exclude them, replace the string below with the names of
# the assemblies to ignore. e.g.,
# $ASSEMBLIES_TO_REMOVE="-AssemblyToRemove1;-AssemblyToRemove2"
$ASSEMBLIES_TO_REMOVE="-KOKTKaraokeParty.Tests"

reportgenerator `
  -reports:"./coverage/**/coverage*.xml" `
  -targetdir:"./coverage/report" `
  "-assemblyfilters:$ASSEMBLIES_TO_REMOVE" `
  "-classfilters:-GodotPlugins.Game.Main;-GameDemo.Main" `
  -reporttypes:"Html"

Invoke-Expression ("cmd /c start coverage/report/index.htm")
