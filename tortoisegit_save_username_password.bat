REM http://www.programmoria.com/2012/02/saving-tortoisegit-password.html

@echo off
SET /P HOST=What is the hostname i.e github.com?
SET /P USERNAME=What is your username?
SET /P PASSWORD=What is your password?
 
echo machine %HOST% login %USERNAME% password %PASSWORD% > "%USERPROFILE%\_netrc"