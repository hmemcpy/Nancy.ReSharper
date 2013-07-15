@echo off
setlocal enableextensions

mkdir InternalsVisibleTo.7.1 2> NUL
copy /y ..\output\Release\7.1\*.* InternalsVisibleTo.7.1\
