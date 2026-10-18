# Run in a regular Windows PowerShell terminal. The App ID is prompted, never passed on the command line.
$ErrorActionPreference = 'Stop'
if (-not ('System.Security.Cryptography.ProtectedData' -as [type])) {
    try { Add-Type -AssemblyName System.Security.Cryptography.ProtectedData }
    catch { Add-Type -AssemblyName System.Security }
}
$photonSecret = Read-Host 'Photon Fusion App ID (entrada oculta)' -AsSecureString
$photonPointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($photonSecret)
$photonBytes = $null
try {
    $photonText = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($photonPointer).Trim()
    $photonGuid = [guid]::Empty
    if (-not [guid]::TryParse($photonText, [ref]$photonGuid)) { throw 'El App ID debe tener formato GUID.' }
    $photonBytes = [System.Text.Encoding]::UTF8.GetBytes($photonText)
    $photonEncrypted = [System.Security.Cryptography.ProtectedData]::Protect(
        $photonBytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    $photonDirectory = Join-Path ([System.Environment]::GetFolderPath('LocalApplicationData')) 'GameJamPlus26\Photon'
    New-Item -ItemType Directory -Path $photonDirectory -Force | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $photonDirectory 'appid.dpapi'), $photonEncrypted)
    Write-Output 'Configuración de Photon guardada cifrada para este usuario de Windows. No se modificaron assets ni archivos del repositorio.'
}
finally {
    if ($null -ne $photonBytes) { [System.Array]::Clear($photonBytes, 0, $photonBytes.Length) }
    $photonText = $null
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($photonPointer)
    $photonSecret.Dispose()
}
