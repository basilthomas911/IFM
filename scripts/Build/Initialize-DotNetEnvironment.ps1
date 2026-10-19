# NuGet uses these Windows paths directly. IDE/automation parents can omit them.
# Preserve explicit values, including redirected roaming profiles and custom installations.
$ErrorActionPreference = 'Stop'
$knownFolders = @{
    'APPDATA' = [Environment+SpecialFolder]::ApplicationData
    'ProgramFiles(x86)' = [Environment+SpecialFolder]::ProgramFilesX86
}
foreach ($name in $knownFolders.Keys) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name, 'Process'))) {
        $path = [Environment]::GetFolderPath($knownFolders[$name])
        if ([string]::IsNullOrWhiteSpace($path)) {
            throw "Windows could not resolve '$name'; NuGet requires this folder to restore packages."
        }
        [Environment]::SetEnvironmentVariable($name, $path, 'Process')
    }
}
