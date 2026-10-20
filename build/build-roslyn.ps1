# Builds MD3inMD6.dll with the Roslyn compiler that ships inside PowerShell 7,
# for machines without the .NET SDK. Normal builds should use `dotnet build`.
#
#   pwsh build/build-roslyn.ps1 -Managed <dir with Assembly-CSharp.dll> `
#        -RefAsm <.NET 4.x reference assemblies, e.g. mono/reference-assemblies/v4.7.1> `
#        -BepInExCore <BepInEx/core folder from a BepInEx 5 release>
#
# If -Managed has no UnityEngine.CoreModule.dll, small stand-ins from build/stubs are
# compiled and used instead (only for compiling; the game's real ones are used at runtime).
param(
  [Parameter(Mandatory)] [string]$Managed,
  [Parameter(Mandatory)] [string]$RefAsm,
  [Parameter(Mandatory)] [string]$BepInExCore,
  [string]$Out = "$PSScriptRoot/../dist/MD3inMD6.dll"
)
$ErrorActionPreference = "Stop"
$pw = Split-Path ([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
Add-Type -Path "$pw/Microsoft.CodeAnalysis.dll"
Add-Type -Path "$pw/Microsoft.CodeAnalysis.CSharp.dll"

function Compile($name, $sources, $refs, $out) {
  $po = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::new([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp7_3)
  $trees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
  foreach ($s in $sources) { $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($s), $po, $s)) }
  $mrefs = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
  foreach ($r in $refs) { $mrefs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($r)) }
  $opt = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release)
  $c = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create($name, $trees, $mrefs, $opt)
  New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
  $fs = [IO.File]::Create($out); $res = $c.Emit($fs); $fs.Close()
  foreach ($d in $res.Diagnostics) {
    if ($d.Id -ne "CS1701" -and $d.Severity -ge [Microsoft.CodeAnalysis.DiagnosticSeverity]::Warning) { Write-Host $d.ToString() }
  }
  if (-not $res.Success) { Remove-Item $out; throw "compile failed: $name" }
  Write-Host "built $out"
}

$base = @("$RefAsm/mscorlib.dll", "$RefAsm/System.dll", "$RefAsm/System.Core.dll", "$RefAsm/Facades/netstandard.dll")
$unity = @()
if (Test-Path "$Managed/UnityEngine.CoreModule.dll") {
  $unity = @("$Managed/UnityEngine.CoreModule.dll", "$Managed/UnityEngine.dll")
} else {
  $obj = "$PSScriptRoot/../obj/stubs"
  Compile "UnityEngine.CoreModule" @("$PSScriptRoot/stubs/UnityEngine.CoreModule.cs") $base "$obj/UnityEngine.CoreModule.dll"
  Compile "UnityEngine" @("$PSScriptRoot/stubs/UnityEngine.cs") ($base + "$obj/UnityEngine.CoreModule.dll") "$obj/UnityEngine.dll"
  $unity = @("$obj/UnityEngine.CoreModule.dll", "$obj/UnityEngine.dll")
}
$refs = $base + $unity + @("$BepInExCore/BepInEx.dll", "$BepInExCore/0Harmony.dll", "$Managed/Assembly-CSharp.dll")
Compile "MD3inMD6" @("$PSScriptRoot/../src/MD3inMD6/MD3inMD6.cs") $refs $Out
