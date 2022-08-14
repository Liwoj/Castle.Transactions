#!/bin/bash
# ****************************************************************************
# Copyright 2004-2022 Castle Project - https://www.castleproject.org/
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
# ****************************************************************************


shopt -s expand_aliases

DOTNETPATH=$(which dotnet)
if [ ! -f "$DOTNETPATH" ]; then
	echo "Please install Microsoft .NET from: https://dotnet.microsoft.com/en-us/download"
	exit 1
fi

DOCKERPATH=$(which docker)
if [ -f "$DOCKERPATH" ]; then
	alias mono="$PWD/buildscripts/docker-run-mono.sh"
else
	MONOPATH=$(which mono)
	if [ ! -f "$MONOPATH" ]; then
		echo "Please install either Docker or Xamarin/Mono from https://www.mono-project.com/docs/getting-started/install/"
		exit 1
	fi
fi

mono --version

# Linux/Darwin
OSNAME=$(uname -s)
echo "OSNAME: $OSNAME"

dotnet build ".\Castle.Transactions.sln" --configuration Release || exit 1

echo ------------------------------------
echo Running .NET (net6.0) Unit Tests
echo ------------------------------------

dotnet ".\src\Castle.Services.Transaction.Tests\bin\Release\net6.0\Castle.Services.Transaction.Tests.dll" --result "Castle.Services.Transaction.Tests-Net-TestResults.xml;format=nunit3" || EXIT /B 1
dotnet ".\src\Castle.Facilities.AutoTx.Tests\bin\Release\net6.0\Castle.Facilities.AutoTx.Tests.dll" --result "Castle.Facilities.AutoTx.Tests-Net-TestResults.xml;format=nunit3" || EXIT /B 1

echo --------------------------------------------
echo Running .NET Framework (net48) Unit Tests
echo --------------------------------------------

mono ".\src\Castle.Services.Transaction.Tests\bin\Release\net48\Castle.Services.Transaction.Tests.exe" --result "Castle.Services.Transaction.Tests-NetFramework-TestResults.xml;format=nunit3" || EXIT /B 1
mono ".\src\Castle.Facilities.AutoTx.Tests\bin\Release\net48\Castle.Facilities.AutoTx.Tests.exe" --result "Castle.Facilities.AutoTx.Tests-NetFramework-TestResults.xml;format=nunit3" || EXIT /B 1

# Ensure that all unit test runs produced protocol files.
if [[ !( -f Castle.Services.Transaction.Tests-Net-TestResults.xml &&
         -f Castle.Facilities.AutoTx.Tests-Net-TestResults.xml &&
         -f Castle.Services.Transaction.Tests-NetFramework-TestResults.xml &&
         -f Castle.Facilities.AutoTx.Tests-NetFramework-TestResults.xml ) ]]; then
    echo "Incomplete test results. Some test runs might not have terminated properly. Failing the build."
    exit 1
fi

# Unit Test Failures
NET_FAILCOUNT=$(grep -F "One or more child tests had errors." Castle.Services.Transaction.Tests-Net-TestResults.xml Castle.Facilities.AutoTx.Tests-Net-TestResults.xml | wc -l)
if [ $NET_FAILCOUNT -ne 0 ]
then
    echo ".NET (net6.0) Unit Tests have failed, failing the build."
    exit 1
fi

NETFRAMEWORK_FAILCOUNT=$(grep -F "One or more child tests had errors." Castle.Services.Transaction.Tests-NetFramework-TestResults.xml Castle.Facilities.AutoTx.Tests-NetFramework-TestResults.xml | wc -l)
if [ $NETFRAMEWORK_FAILCOUNT -ne 0 ]
then
    echo ".NET Framework (net48) Unit Tests have failed, failing the build."
    exit 1
fi



