#!/usr/bin/env pwsh

# SPDX-FileCopyrightText: 2023 aas-package3-csharp AUTHORS
#
# SPDX-License-Identifier: MIT

<#
.SYNOPSIS
This script inspects the code quality after the build.
#>

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot Common.psm1) -Function `
    CreateAndGetArtefactsDir

function Main
{
    $artefactsDir = CreateAndGetArtefactsDir
    $codeInspectionPath = Join-Path $artefactsDir "resharper-code-inspection.json"

    Set-Location $PSScriptRoot

    Write-Host "Inspecting the code with inspectcode ..."

    $cachesHome = Join-Path $artefactsDir "inspectcode-caches"
    New-Item -ItemType Directory -Force -Path "$cachesHome"|Out-Null

    # InspectCode passes over the properties to MSBuild,
    # see https://www.jetbrains.com/help/resharper/InspectCode.html#msbuild-related-parameters
    #
    # We inspect only the net10.0 build. The code is the same for all target
    # frameworks, but the net462 build of System.IO.Packaging has no nullable
    # annotations, which results in false positives about possible null
    # references.
    & dotnet jb inspectcode `
        "--properties:Configuration=DebugSlow;TargetFramework=net10.0" `
        "-o=$codeInspectionPath" `
        "--caches-home=$cachesHome" `
        '--exclude=*\obj\*;packages\*;*\bin\*;*\*.json;*\TestResources\*' `
        aas-package3-csharp.sln

    $sarif = Get-Content $codeInspectionPath -Raw | ConvertFrom-Json

    $results = $sarif.runs[0].results

    if ($results.Count -ne 0) {
        # Compute histogram of issue types
        $histogram = @{}
        foreach ($result in $results) {
            $ruleId = $result.ruleId
            if ($histogram.ContainsKey($ruleId)) {
                $histogram[$ruleId]++
            } else {
                $histogram[$ruleId] = 1
            }
        }

        Write-Host
        Write-Host "The distribution of the issues:"
        foreach ($kv in $histogram.GetEnumerator() | Sort-Object Value -Descending) {
            Write-Host (" * {0,-60} {1,6}" -f ($kv.Key + ":"), $kv.Value)
        }

        # Display a few issues
        $take = [Math]::Min(20, $results.Count)

        Write-Host
        Write-Host "The first $take issue(s):"
        for ($i = 0; $i -lt $take; $i++) {
            $result = $results[$i]
            $msg = $result.message.text
            $file = $result.locations[0].physicalLocation.artifactLocation.uri
            $line = $result.locations[0].physicalLocation.region.startLine
            Write-Host "Issue $($i + 1) / $($results.Count): $msg (${file}:${line})"
        }

        if ($take -lt $results.Count) {
            Write-Host "... and some more issues ($($results.Count) in total)."
        }

        throw (
            "There are $($results.Count) InspectCode issue(s). " +
            "The issues are stored in: $codeInspectionPath. " +
            "Please fix the issues using an IDE like Rider or Visual Studio, or refer to: " +
            "https://www.jetbrains.com/help/resharper/Reference__Code_Inspections_CSHARP.html#BestPractice"
        )
    } else {
        Write-Host "There were no issues detected."
    }
}

$previousLocation = Get-Location; try { Main } finally { Set-Location $previousLocation }

