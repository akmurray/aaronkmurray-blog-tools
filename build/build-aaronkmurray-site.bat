@echo off
REM Do all of the prep-work steps required to build the aaronkmurray.com site

REM use RSSGEN to build rss feed
rssgen.exe -s=../../aaronkmurray-blog/index.html -o=../../aaronkmurray-blog/feeds/feed-rss.xml -f=rss

REM use RSSGEN to build atom feed
rssgen.exe -s=../../aaronkmurray-blog/index.html -o=../../aaronkmurray-blog/feeds/feed-atom.xml -f=atom

REM use imgsqz to losslessly compress the filesize of images
imgsqz.exe -s=../../aaronkmurray-blog/ -debug


ECHO ............................................
ECHO Build Complete at %date% %time%
ECHO ............................................

REM just pause the screen so we can see the output (remove after you get the point)
pause