#!/usr/bin/env pwsh

# SPDX-FileCopyrightText: 2023 aas-package3-csharp AUTHORS
#
# SPDX-License-Identifier: MIT

<#
.SYNOPSIS
    This script runs all the unit tests.
#>

$ErrorActionPreference = "Stop"

function Main
{
    & dotnet test -c DebugSlow
    if ($LASTEXITCODE -ne 0)
    {
        throw "The unit tests failed."
    }
}

$previousLocation = Get-Location; try
{
    Main
}
finally
{
    Set-Location $previousLocation
}
