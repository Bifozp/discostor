#!/usr/bin/bash

# Move into src directory
PROJECT_DIR=src
SCRIPT_DIR=$(cd $(dirname $0) && pwd)
cd $SCRIPT_DIR

: ${BUILD_TYPE:=Debug}
: ${BUILD_OPTIONS:=""}
BUILD_TYPE_FLAGS=""
PUBLISH_DIR=""
PROJECT=""
VERBOSE_OPTION=""
GEN_ZIP=0
CLEAN_BUILD=0

function _command(){
	echo "$@"
	$@
}

function Configure(){
    while [ $# -gt 0 ]; do
        case "$1" in
            -c) CLEAN_BUILD=1;;
            -d) BUILD_TYPE="Debug"
                BUILD_TYPE_FLAGS="";;
            -r) BUILD_TYPE="Release"
                BUILD_TYPE_FLAGS="-p:DebugType=none";;
            -z) GEN_ZIP=1 ;;
            -v) VERBOSE_OPTION="-v n";;
            *) break;;
        esac
        shift
    done
    PROJECT=$(basename *.csproj .csproj|head -n 1)
    PUBLISH_DIR=bin/$BUILD_TYPE/net5.0/publish
}

function Publish(){
    if [ $CLEAN_BUILD -ne 0 ]; then
        echo "Cleaning..."
        _command rm -rf bin/$BUILD_TYPE obj/$BUILD_TYPE
    fi
    echo "Building... (Type: $BUILD_TYPE)"
    _command rm -rf $PUBLISH_DIR
    _command dotnet publish -nologo -c $BUILD_TYPE $BUILD_TYPE_FLAGS $BUILD_OPTIONS $VERBOSE_OPTION
    err=$?
    if [ $err -ne 0 ]; then
        echo "build failed... (Error code: $err)"
        return 1
    fi
    return 0
}

function Organize(){
    if [ ! -d $PUBLISH_DIR ]; then
        echo "publish dir doesn't exist..."
        return 1
    fi
    
    _command cd $PUBLISH_DIR
    _command mkdir plugins
    _command mkdir libraries
    _command mkdir config

    # delete
    echo "Removing modules contained in Impostor.Server..."
    cat $SCRIPT_DIR/Impostor.Server.Modules.txt|xargs rm -f
    _command rm -f $PROJECT.deps.json

    # Move files
    _command mv $PROJECT.dll plugins/
    _command mv ${PROJECT,,}.json ${PROJECT,,}-emotes.json config/
    _command mv *.dll libraries/
    _command cd -

    return 0
}

function GenerateZip(){
    _command cd $PUBLISH_DIR
    echo "generating zip..."
    _command zip -r $SCRIPT_DIR/$PROJECT.zip *
}

_command cd $PROJECT_DIR
Configure $@
if Publish && Organize; then
    [ $GEN_ZIP -ne 0 ] && GenerateZip
    echo "done."
fi
