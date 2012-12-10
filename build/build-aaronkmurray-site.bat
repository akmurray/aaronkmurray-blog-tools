@ECHO OFF
REM Do all of the prep-work steps required to build the aaronkmurray.com site 



REM --------------------------------------------------------------------------
REM Run Javascript Unit Tests - fail build if necessary
REM --------------------------------------------------------------------------

SET phantomjsErrFile=_phantomjs_results.txt
phantomjs.exe --web-security=false ../../aaronkmurray-blog/test/jasmine-standalone-1.2.0/lib/phantom-jasmine/run-jasmine-test.coffee ../../aaronkmurray-blog/test/jasmine-standalone-1.2.0/SpecRunner.html > %phantomjsErrFile%
IF ERRORLEVEL 2 GOTO phantomjs_errors
IF ERRORLEVEL 1 GOTO phantomjs_errors
IF ERRORLEVEL 0 GOTO phantomjs_success

:phantomjs_errors
ECHO phantomjs: errors found
notepad %phantomjsErrFile%
exit

:phantomjs_success
ECHO phantomjs: success

:phantomjs_post_success




REM --------------------------------------------------------------------------
REM Run Tidy to check for HTML warnings/errors and fail build if necessary
REM	http://tidy.sourceforge.net/
REM	ALT? http://home.ccil.org/~cowan/XML/tagsoup/
REM --------------------------------------------------------------------------

SET tidyErrFile=_tidy.index.html.errors.txt
tidy.exe -output _tidy.index.html.original.txt -file %tidyErrFile%  ../../aaronkmurray-blog/index.html

IF ERRORLEVEL 2 GOTO tidy_errors
IF ERRORLEVEL 1 GOTO tidy_warnings
IF ERRORLEVEL 0 GOTO tidy_success

:tidy_errors
ECHO tidy: errors found
notepad %tidyErrFile%
exit

:tidy_warnings
ECHO tidy: Warnings found. Review the notepad file and close it to continue processing.
notepad %tidyErrFile%
REM don't kill the build on warnings, but show what they are 
GOTO :tidy_post_success

:tidy_success
ECHO tidy: success

:tidy_post_success




REM --------------------------------------------------------------------------
REM Generate small post thumbnails
REM --------------------------------------------------------------------------

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




REM --------------------------------------------------------------------------
REM Create a CSS Sprite for small post thumbs
REM --------------------------------------------------------------------------

REM use imgsprite to make a css sprite for the thumbnail previews
imgsprite.exe -in:../../aaronkmurray-blog/img/blog/screenshots/*-thumb-100*.png -img-out:../../aaronkmurray-blog/img/blog/sprites/post-screenshot-thumbs-all.png -css-out:../../aaronkmurray-blog/css/sprites/post-screenshot-thumbs-all.css -css-class-name-prefix:img- -image-deploy-url-base:../../img/blog/sprites/ -gen-test-html:true -test-html-path:../../aaronkmurray-blog/test/sprites/ -test-html-deploy-url-base:../../img/blog/sprites/ -limit-bit-depth:8 

REM use imgsprite to make a css sprite for site header & footer icons
imgsprite.exe -in:../../aaronkmurray-blog/img/blog/icons/*.png -img-out:../../aaronkmurray-blog/img/blog/sprites/blog-icons-all.png -css-out:../../aaronkmurray-blog/css/sprites/blog-icons-all.css -css-class-name-prefix:img- -image-deploy-url-base:../../img/blog/sprites/ -gen-test-html:true -test-html-path:../../aaronkmurray-blog/test/sprites/ -test-html-deploy-url-base:../../img/blog/sprites/

REM just here as a stub example for reducing an image to 8bpp
REM imgsprite.exe -in:../../aaronkmurray-blog/img/blog/posts/post-22-speed-affects-consumers.png -img-out:../../aaronkmurray-blog/img/blog/posts/post-22-speed-affects-consumers-8.png -css-out:delete_me.css -css-class-name-prefix:img- -image-deploy-url-base:/ -gen-test-html:false -limit-bit-depth:8 




REM --------------------------------------------------------------------------
REM Run CSSLint (via Rhino) to check for CSS warnings/errors and fail build if necessary
REM	https://github.com/stubbornella/csslint
REM --------------------------------------------------------------------------

SET csslintErrFile=_csslint.errors.txt
"C:\Program Files (x86)\Java\jre7\bin\java" -jar rhino.jar csslint-rhino.js --ignore=ids,qualified-headings,unique-headings,duplicate-background-images,zero-units,outline-none,empty-rules,overqualified-elements --warnings=universal-selector ../../aaronkmurray-blog/css/ > %csslintErrFile%

IF ERRORLEVEL 2 GOTO csslint_errors
IF ERRORLEVEL 1 GOTO csslint_warnings
IF ERRORLEVEL 0 GOTO csslint_success

:csslint_errors
ECHO csslint: errors found
notepad %csslintErrFile%
exit

:csslint_warnings
ECHO csslint: Warnings found. Review the notepad file and close it to continue processing.
notepad %csslintErrFile%
REM don't kill the build on warnings, but show what they are 
GOTO :csslint_post_success

:csslint_success
ECHO csslint: success

:csslint_post_success




REM --------------------------------------------------------------------------
REM Generate RSS/ATOM feeds and fail build if necessary
REM --------------------------------------------------------------------------

REM use RSSGEN to build rss feed
rssgen.exe -s=../../aaronkmurray-blog/index.html -o=../../aaronkmurray-blog/feeds/feed-rss.xml -f=rss

IF ERRORLEVEL 2 GOTO rssgen_error
IF ERRORLEVEL 1 GOTO rssgen_warning
IF ERRORLEVEL 0 GOTO rssgen_xml_success


:rssgen_xml_success
REM use RSSGEN to build atom feed
rssgen.exe -s=../../aaronkmurray-blog/index.html -o=../../aaronkmurray-blog/feeds/feed-atom.xml -f=atom

IF ERRORLEVEL 2 GOTO rssgen_error
IF ERRORLEVEL 1 GOTO rssgen_warning
IF ERRORLEVEL 0 GOTO rssgen_atom_success


:rssgen_error
:rssgen_warning
ECHO rssgen: errors found
exit


:rssgen_atom_success




REM --------------------------------------------------------------------------
REM Compress images to save bandwidth
REM --------------------------------------------------------------------------

REM use imgsqz to losslessly compress the filesize of images
imgsqz.exe -s=../../aaronkmurray-blog/ 




REM --------------------------------------------------------------------------
REM Testing area for future stuff
REM --------------------------------------------------------------------------

REM Future (just testing for now). HTML minification nets approx 10% filesize reduction. 
REM Straight gzip on unminified HTML went from 59KB to 18KB...so we can realistically expect this to only have a 1% actual improvement
REM http://code.google.com/p/htmlcompressor/
REM 59->55KB   java -jar htmlcompressor-1.5.3.jar -o ../../aaronkmurray-blog/index.min.html ../../aaronkmurray-blog/index.html
REM 59->54.5KB   java -jar htmlcompressor-1.5.3.jar --preserve-line-breaks -o ../../aaronkmurray-blog/index.min.html ../../aaronkmurray-blog/index.html
REM 59->54KB   java -jar htmlcompressor-1.5.3.jar --preserve-line-breaks --remove-quotes --remove-intertag-spaces -o ../../aaronkmurray-blog/index.min.html ../../aaronkmurray-blog/index.html
REM 59->53.5KB   java -jar htmlcompressor-1.5.3.jar --preserve-line-breaks --remove-quotes --remove-intertag-spaces --remove-http-protocol --remove-surrounding-spaces all -o ../../aaronkmurray-blog/index.min.html ../../aaronkmurray-blog/index.html


REM --------------------------------------------------------------------------
REM COMPLETE!
REM --------------------------------------------------------------------------

ECHO ............................................
ECHO Build Complete at %date% %time%
ECHO ............................................

REM just pause the screen so we can see the output (remove after you get the point)
pause