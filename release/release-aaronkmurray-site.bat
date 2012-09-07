REM Release the latest version of AaronKMurray.com from source control

cd c:\code\git\aaronkmurray-blog
git pull https://github.com/akmurray/aaronkmurray-blog
xcopy /Y /E /R /V /I "c:\code\git\aaronkmurray-blog" "C:\inetpub\wwwroot\aaronkmurray" 

