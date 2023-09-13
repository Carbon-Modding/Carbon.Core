::
:: Copyright (c) 2022-2023 Carbon Community 
:: All rights reserved
::
@echo off

set HOME=%cd%
set TEMP=%~dp0..\..\..\Carbon.Core\.tmp

if not exist %TEMP% (
	mkdir %TEMP%
)

echo ** Git Metadata:

cd %TEMP%
git branch --show-current > .gitbranch
echo **   Branch done.

git rev-parse --short HEAD > .gitchs
echo **   Hash-Long done.

git rev-parse --long HEAD > .gitchl
echo **   Hash-Long done.

git show -s --format=%%%an HEAD > .gitauthor
echo **   Author done.

git log -1 --pretty=%%%B > .gitcomment
echo **   Comment done.

git log -1 --format=%%%ci HEAD > .gitdate
echo **   Date done.

git remote get-url origin > .giturl
echo **   URL done.

git log -1 --name-status --format= > .gitchanges
echo **   Changes done.

cd %HOME%