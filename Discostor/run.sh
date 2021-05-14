#!/usr/bin/bash
set -e
SCRIPT_DIR=$(cd $(dirname $0) && pwd)
cd $SCRIPT_DIR

PROJECT_DIR="$SCRIPT_DIR/src"
IMPOSTOR_SERVER_PROJECT_DIR="$SCRIPT_DIR/../Impostor/src/Impostor.Server"

: ${IMPOSTOR_Server__PublicIp:=127.0.0.1}
: ${IMPOSTOR_Server__PublicPort:=22023}
: ${IMPOSTOR_Server__ListenIp:=0.0.0.0}
: ${IMPOSTOR_Server__ListenPort:=$IMPOSTOR_Server__PublicPort}
: ${IMPOSTOR_AnnouncementsServer__Enabled:=false}
: ${IMPOSTOR_AnnouncementsServer__ListenIp:=0.0.0.0}
: ${IMPOSTOR_AnnouncementsServer__ListenPort:=22024}
: ${IMPOSTOR_ServerRedirector__Enabled:=false}
: ${IMPOSTOR_AntiCheat__Enabled:=false}
: ${IMPOSTOR_AntiCheat__BanIpFromGame:=false}

: ${BUILD_TYPE:=Debug}
: ${BUILD_OPTIONS:=""}

BUILD_TYPE_FLAGS=""
PUBLISH_DIR=""
PROJECT=""
VERBOSE_OPTION=""
CLEAN_BUILD=0

function _command(){
	echo "$@"
	$@
}

function _Clean(){
    echo "Cleaning..."
    _command rm -rf bin/$BUILD_TYPE obj/$BUILD_TYPE
}

function Configure(){
    while [ $# -gt 0 ]; do
        case "$1" in
            -c) CLEAN_BUILD=1;;
            -d) BUILD_TYPE="Debug"
                BUILD_TYPE_FLAGS="";;
            -r) BUILD_TYPE="Release"
                BUILD_TYPE_FLAGS="-p:DebugType=none";;
            -v) VERBOSE_OPTION="-v n";;
            *) break;;
        esac
        shift
    done
    PROJECT=$(basename *.csproj .csproj|head -n 1)
    PUBLISH_DIR=$PROJECT_DIR/bin/$BUILD_TYPE/net5.0/publish
}

function Publish(){
    [ $CLEAN_BUILD -ne 0 ] && _Clean
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

function Run(){
    #_command cd "$IMPOSTOR_SERVER_PROJECT_DIR"
    #[ $CLEAN_BUILD -ne 0 ] && _Clean
    : ${IMPOSTOR_PluginLoader__Paths__0:=$PUBLISH_DIR/..}
    : ${IMPOSTOR_PluginLoader__LibraryPaths__0:=$PUBLISH_DIR}

    export IMPOSTOR_Server__PublicIp
    export IMPOSTOR_Server__PublicPort
    export IMPOSTOR_Server__ListenIp
    export IMPOSTOR_Server__ListenPort
    export IMPOSTOR_AnnouncementsServer__Enabled
    export IMPOSTOR_ServerRedirector__Enabled
    export IMPOSTOR_AntiCheat__Enabled
    export IMPOSTOR_AntiCheat__BanIpFromGame
    export IMPOSTOR_PluginLoader__Paths__0
    export IMPOSTOR_PluginLoader__LibraryPaths__0
    export IMPOSTOR_AnnouncementsServer__ListenIp
    export IMPOSTOR_AnnouncementsServer__ListenPort
    _command dotnet run -nologo -p "$IMPOSTOR_SERVER_PROJECT_DIR" -c $BUILD_TYPE $BUILD_TYPE_FLAGS $BUILD_OPTIONS $VERBOSE_OPTION | tee -i latest.log
    cd -
}


_command cd $PROJECT_DIR
Configure $@
if Publish ; then
    _command cd $SCRIPT_DIR
    Run
    echo "done."
fi
