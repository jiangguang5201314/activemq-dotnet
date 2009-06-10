# Licensed to the Apache Software Foundation (ASF) under one or more
# contributor license agreements.  See the NOTICE file distributed with
# this work for additional information regarding copyright ownership.
# The ASF licenses this file to You under the Apache License, Version 2.0
# (the "License"); you may not use this file except in compliance with
# the License.  You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

$pkgname = "Apache.NMS"
$pkgver = "1.1.0"
$configurations = "release", "debug"
$frameworks = "mono-2.0", "net-1.1", "net-2.0", "net-3.5", "netcf-2.0"

function package-legalfiles($zipfile)
{
	zip -9 -u -j "$zipfile" ..\LICENSE.txt
	zip -9 -u -j "$zipfile" ..\NOTICE.txt
}

write-progress "Creating package directory." "Initializing..."
if(!(test-path package))
{
	md package
}

pushd build

$pkgdir = "..\package"

write-progress "Packaging Application files." "Scanning..."
foreach($configuration in $configurations)
{
	$zipfile = "$pkgdir\$pkgname-$pkgver-bin-$configuration.zip"
	package-legalfiles $zipfile
	foreach($framework in $frameworks)
	{
		zip -9 -u "$zipfile" "$framework\$configuration\$pkgname.dll"
	}
}

write-progress "Packaging PDB files." "Scanning..."
foreach($configuration in $configurations)
{
	$zipfile = "$pkgdir\$pkgname-$pkgver-PDBs-$configuration.zip"
	package-legalfiles $zipfile
	foreach($framework in $frameworks)
	{
		if($framework -ieq "mono-2.0")
		{
			zip -9 -u "$zipfile" "$framework\$configuration\$pkgname.dll.mdb"
		}
		else
		{
			zip -9 -u "$zipfile" "$framework\$configuration\$pkgname.pdb"
		}
	}
}

write-progress "Packaging Unit test files." "Scanning..."
foreach($configuration in $configurations)
{
	$zipfile = "$pkgdir\$pkgname-$pkgver-UnitTests-$configuration.zip"
	package-legalfiles $zipfile
	foreach($framework in $frameworks)
	{
		zip -9 -u "$zipfile" "$framework\$configuration\$pkgname.Test.dll"
		if($framework -ieq "mono-2.0")
		{
			zip -9 -u "$zipfile" "$framework\$configuration\$pkgname.Test.dll.mdb"
		}
		else
		{
			zip -9 -u "$zipfile" "$framework\$configuration\$pkgname.Test.pdb"
		}
	}
}

popd

write-progress "Packaging Source code files." "Scanning..."
$pkgdir = "package"
$zipfile = "$pkgdir\$pkgname-$pkgver-src.zip"

zip -9 -u "$zipfile" LICENSE.txt NOTICE.txt nant-common.xml nant.build package.ps1 vs2008-nms-test.csproj vs2008-nms.csproj vs2008-nms.sln
zip -9 -u -r "$zipfile" keyfile src

write-progress "Packaging" "Complete."