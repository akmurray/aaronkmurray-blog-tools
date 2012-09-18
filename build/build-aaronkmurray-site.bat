@ECHO OFF
REM Do all of the prep-work steps required to build the aaronkmurray.com site 


REM Make post screenshot thumbnails if necessary
FOR /F %%A IN ('dir /b "../../aaronkmurray-blog/img/blog/screenshots/" ^|findstr /liv "thumb"') DO (

  REM so that we can update variables inside a loop. Those variables are wrapped in "!" instead of "%"
  SETLOCAL ENABLEDELAYEDEXPANSION
  
  SET thumbName=%%~nA-thumb-100.png
  SET thumbPath=../../aaronkmurray-blog/img/blog/screenshots/
  SET thumbPathAndName=!thumbPath!!thumbName!
  
  IF NOT EXIST !thumbPathAndName! (
    ECHO Creating thumbnail for !thumbName!
    convert.exe -thumbnail 100 !thumbPath!%%A !thumbPathAndName!
  ) ELSE (
    REM ECHO Already created thumbnail for !thumbName!
  )
)


REM use RSSGEN to build rss feed
rssgen.exe -s=../../aaronkmurray-blog/index.html -o=../../aaronkmurray-blog/feeds/feed-rss.xml -f=rss


REM use RSSGEN to build atom feed
rssgen.exe -s=../../aaronkmurray-blog/index.html -o=../../aaronkmurray-blog/feeds/feed-atom.xml -f=atom


REM use imgsqz to losslessly compress the filesize of images
imgsqz.exe -s=../../aaronkmurray-blog/ -debug


REM Future (just testing for now). HTML minification nets approx 10% filesize reduction. 
REM Straight gzip on unminified HTML went from 59KB to 18KB...so we can realistically expect this to only have a 1% actual improvement
REM http://code.google.com/p/htmlcompressor/
REM 59->55KB   java -jar htmlcompressor-1.5.3.jar -o ../../aaronkmurray-blog/index.min.html ../../aaronkmurray-blog/index.html
REM 59->54.5KB   java -jar htmlcompressor-1.5.3.jar --preserve-line-breaks -o ../../aaronkmurray-blog/index.min.html ../../aaronkmurray-blog/index.html
REM 59->54KB   java -jar htmlcompressor-1.5.3.jar --preserve-line-breaks --remove-quotes --remove-intertag-spaces -o ../../aaronkmurray-blog/index.min.html ../../aaronkmurray-blog/index.html
REM 59->53.5KB   java -jar htmlcompressor-1.5.3.jar --preserve-line-breaks --remove-quotes --remove-intertag-spaces --remove-http-protocol --remove-surrounding-spaces all -o ../../aaronkmurray-blog/index.min.html ../../aaronkmurray-blog/index.html


ECHO ............................................
ECHO Build Complete at %date% %time%
ECHO ............................................

REM just pause the screen so we can see the output (remove after you get the point)
pause