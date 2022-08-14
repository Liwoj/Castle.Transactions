@ECHO OFF
REM ****************************************************************************
REM Copyright 2004-2022 Castle Project - https://www.castleproject.org/
REM Licensed under the Apache License, Version 2.0 (the "License");
REM you may not use this file except in compliance with the License.
REM You may obtain a copy of the License at
REM
REM     http://www.apache.org/licenses/LICENSE-2.0
REM
REM Unless required by applicable law or agreed to in writing, software
REM distributed under the License is distributed on an "AS IS" BASIS,
REM WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
REM See the License for the specific language governing permissions and
REM limitations under the License.
REM ****************************************************************************


SET BUILD_CONFIGURATION=Release
SET BUILD_VERSION=1.0.0

GOTO SET_BUILD_CONFIGURATION


:SET_BUILD_CONFIGURATION
IF "%configuration%"=="" GOTO SET_BUILD_VERSION
SET BUILD_CONFIGURATION=%configuration%

GOTO SET_BUILD_VERSION


:SET_BUILD_VERSION
IF "%version%"=="" GOTO BUILD
SET BUILD_VERSION=%version%

GOTO BUILD


:BUILD

ECHO ----------------------------------------------------
ECHO Building "%BUILD_CONFIGURATION%" packages with version "%BUILD_VERSION%"...
ECHO ----------------------------------------------------

dotnet build ".\Castle.Transactions.sln" --configuration %BUILD_CONFIGURATION% -property:APPVEYOR_BUILD_VERSION=%BUILD_VERSION% || EXIT /B 1

dotnet build ".\tools\Explicit.NuGet.Versions\Explicit.NuGet.Versions.sln" --configuration Release || EXIT /B 1
".\tools\Explicit.NuGet.Versions\bin\nev.exe" ".\build" "Castle." || EXIT /B 1

GOTO TEST


:TEST

REM https://github.com/Microsoft/vstest-docs/blob/main/docs/report.md
REM https://github.com/spekt/nunit.testlogger/issues/56

ECHO ------------------------------------
ECHO Running .NET (net6.0) Unit Tests
ECHO ------------------------------------

dotnet ".\src\Castle.Services.Transaction.Tests\bin\%BUILD_CONFIGURATION%\net6.0\Castle.Services.Transaction.Tests.dll" --work ".\build" --result "Castle.Services.Transaction.Tests-Net-TestResults.xml;format=nunit3" || EXIT /B 1
dotnet ".\src\Castle.Facilities.AutoTx.Tests\bin\%BUILD_CONFIGURATION%\net6.0\Castle.Facilities.AutoTx.Tests.dll" --work ".\build" --result "Castle.Facilities.AutoTx.Tests-Net-TestResults.xml;format=nunit3" || EXIT /B 1

ECHO --------------------------------------------
ECHO Running .NET Framework (net48) Unit Tests
ECHO --------------------------------------------

SET "NUNIT_CONSOLE_PATH=%UserProfile%\.nuget\packages\nunit.consolerunner\3.15.2\tools\nunit3-console.exe"

%NUNIT_CONSOLE_PATH% ".\src\Castle.Services.Transaction.Tests\bin\%BUILD_CONFIGURATION%\net48\Castle.Services.Transaction.Tests.exe" --work ".\build" --result "Castle.Services.Transaction.Tests-NetFramework-TestResults.xml;format=nunit3" || EXIT /B 1
%NUNIT_CONSOLE_PATH% ".\src\Castle.Facilities.AutoTx.Tests\bin\%BUILD_CONFIGURATION%\net48\Castle.Facilities.AutoTx.Tests.exe" --work ".\build" --result "Castle.Facilities.AutoTx.Tests-NetFramework-TestResults.xml;format=nunit3" || EXIT /B 1



