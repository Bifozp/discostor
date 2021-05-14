#!/usr/bin/bash
function _cmd(){
    echo "$@"
    $@
}
SCRIPT_DIR=$(cd $(dirname $0) && pwd)
cd $SCRIPT_DIR

_cmd git submodule update --init --recursive --remote --recommend-shallow --depth 1
_cmd cp .sparse-checkout.Impostor ./.git/modules/Impostor/info/sparse-checkout
_cmd cd Impostor
_cmd git config core.sparsecheckout true
_cmd git read-tree -mu HEAD
