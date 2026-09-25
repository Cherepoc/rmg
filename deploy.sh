#!/usr/bin/env bash
#
# Builds RMG, runs its tests, publishes it self-contained and puts it on a VPS behind systemd.
#
# The soundfont is fetched on the VPS rather than pushed from here: it is tens of megabytes, the VPS
# very likely has the better uplink, and it is the one file that must survive a deploy, so the sync
# leaves its directory alone.
#
# Settings come from deploy.env beside this script, from the environment, or from the flags below.

set -euo pipefail

readonly ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

# --- settings --------------------------------------------------------------

# shellcheck source=/dev/null
[[ -f "$ROOT/deploy.env" ]] && source "$ROOT/deploy.env"

HOST="${RMG_HOST:-}"                      # ssh target: an alias from ~/.ssh/config, or user@host
TARGET="${RMG_PATH:-/opt/rmg}"            # where the published app lives on the VPS
SERVICE="${RMG_SERVICE:-rmg}"             # name of the systemd unit
SERVICE_USER="${RMG_SERVICE_USER:-rmg}"   # the account the app runs as, which owns nothing it can write
RUNTIME="${RMG_RUNTIME:-linux-x64}"

# passed to the service as RMG_PORT, and where the health check looks afterwards
PORT="${RMG_PORT:-5000}"

# loopback by default: a VPS deployment belongs behind a reverse proxy, and binding every interface
# would leave the app answering on its port directly, around whatever is meant to be in front of it
ADDRESS="${RMG_ADDRESS:-loopback}"

# the reverse proxy, if it is not on this same machine. Loopback is trusted without being named.
KNOWN_PROXIES="${RMG_KNOWNPROXIES:-}"

# what the analytics dashboard asks for before it shows anything. Empty leaves it switched off.
DASHBOARD_TOKEN="${RMG_DASHBOARDTOKEN:-}"

# these are left out of the settings file altogether when empty, so appsettings.json decides
RETENTION_DAYS="${RMG_RETENTIONDAYS:-}"
DEFAULT_SOUNDFONT="${RMG_DEFAULTSOUNDFONT:-}"
ANALYTICS_ENABLED="${RMG_ANALYTICSENABLED:-}"

# What the VPS fetches for itself, one per line, as "url" or "url|what to call it". A .tar.gz is
# unpacked and the soundfont inside is what gets the name. A licence has to be named so that it lands
# beside the soundfont it belongs to, which is how the page knows to link the two.
SOUNDFONTS="${RMG_SOUNDFONTS:-$(cat <<'LIST'
http://deb.debian.org/debian/pool/main/t/timgm6mb-soundfont/timgm6mb-soundfont_1.3.orig.tar.gz|TimGM6mb.sf2
https://www.gnu.org/licenses/old-licenses/gpl-2.0.txt|TimGM6mb_License.txt
https://ftp.osuosl.org/pub/musescore/soundfont/MuseScore_General/MuseScore_General.sf3
https://ftp.osuosl.org/pub/musescore/soundfont/MuseScore_General/MuseScore_General_License.md
LIST
)}"

install_service=false
run_tests=true
fetch_soundfont=true

# --- arguments -------------------------------------------------------------

usage() {
    cat <<USAGE
Usage: ${0##*/} [options]

Builds, tests, publishes and deploys RMG to a VPS.

Options:
  --host <user@host>   Where to deploy. Required, unless RMG_HOST is set.
  --path <dir>         Where the app goes on the VPS. Default: $TARGET
  --service <name>     Name of the systemd unit. Default: $SERVICE
  --port <port>        Port the app listens on. Default: $PORT. It is written into the unit, so
                       re-run --install after changing it.
  --address <where>    What to listen on: loopback, any, or an IP. Default: $ADDRESS, which means
                       only a proxy on the VPS itself can reach it. Also written into the unit.
  --install            Create the service account and the systemd unit first. Needed once, and
                       harmless afterwards: it rewrites the unit and leaves everything else alone.
  --skip-tests         Publish whatever builds, without running the tests first.
  --no-soundfont       Leave the soundfont directory entirely alone.
  -h, --help           This.

Settings come from deploy.env beside this script. Those named RMG_* are put on the server and
override appsettings.json, which ships with the app and holds the defaults: RMG_PORT, RMG_ADDRESS,
RMG_KNOWNPROXIES, RMG_DASHBOARDTOKEN, RMG_RETENTIONDAYS, RMG_DEFAULTSOUNDFONT and RMG_ANALYTICSENABLED. The rest say where to deploy: RMG_HOST,
RMG_PATH, RMG_SERVICE, RMG_SERVICE_USER, RMG_RUNTIME and RMG_SOUNDFONTS.
USAGE
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --host) HOST="${2:?--host needs a value}"; shift 2 ;;
        --path) TARGET="${2:?--path needs a value}"; shift 2 ;;
        --service) SERVICE="${2:?--service needs a value}"; shift 2 ;;
        --port) PORT="${2:?--port needs a value}"; shift 2 ;;
        --address) ADDRESS="${2:?--address needs a value}"; shift 2 ;;
        --install) install_service=true; shift ;;
        --skip-tests) run_tests=false; shift ;;
        --no-soundfont) fetch_soundfont=false; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
done

# --- output ----------------------------------------------------------------

if [[ -t 1 ]]; then
    readonly BOLD=$'\033[1m' DIM=$'\033[2m' RED=$'\033[31m' PLAIN=$'\033[0m'
else
    readonly BOLD='' DIM='' RED='' PLAIN=''
fi

step() { printf '\n%s==>%s %s%s\n' "$BOLD" "$PLAIN" "$1" "$PLAIN"; }
note() { printf '%s    %s%s\n' "$DIM" "$1" "$PLAIN"; }
die() { printf '\n%sdeploy: %s%s\n' "$RED" "$1" "$PLAIN" >&2; exit 1; }

# --- checks ----------------------------------------------------------------

[[ -n "$HOST" ]] || die "no host. Pass --host user@host, or set RMG_HOST in deploy.env."

for tool in dotnet rsync ssh; do
    command -v "$tool" >/dev/null || die "$tool is not installed."
done

step "Reaching $HOST"
ssh -o BatchMode=yes -o ConnectTimeout=10 "$HOST" true \
    || die "cannot ssh to $HOST without a password. Check your key and ~/.ssh/config."
note "ok"

# --- build and test --------------------------------------------------------

step "Building"
dotnet build "$ROOT/Rmg.slnx" -c Release --nologo -v quiet || die "the build failed."

if $run_tests; then
    step "Running tests"
    # the suite runs on Microsoft.Testing.Platform, so it is a program of its own and runs as one, on the build above
    dotnet run --project "$ROOT/Rmg.Tests" -c Release --no-build || die "the tests failed. Nothing was deployed."
else
    note "skipped, as asked"
fi

# --- publish ---------------------------------------------------------------

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

# One executable with the runtime inside, so the VPS needs no .NET of its own. Beside it are only what
# cannot go in: wwwroot, which is served from disk, and the native SQLite library, which a single file
# would otherwise unpack to a temporary directory on every start, and the service may have none to use.
# Compressed, it is half the size, for a moment's decompression on start.
step "Publishing a single self-contained file for $RUNTIME"
dotnet publish "$ROOT/Rmg.WebApi" \
    -c Release \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none \
    -o "$STAGE" \
    --nologo -v quiet \
    || die "the publish failed."

[[ -x "$STAGE/Rmg.WebApi" ]] || die "published, but there is no Rmg.WebApi executable in the output."

note "$(du -sh "$STAGE" | cut -f1) to send"

# --- provision -------------------------------------------------------------

# every ssh here goes through one script on stdin, so a deploy is a handful of round trips rather than dozens
remote() {
    local quoted=""
    for argument in "$@"; do quoted+=" $(printf '%q' "$argument")"; done

    ssh -o BatchMode=yes "$HOST" "bash -euo pipefail -s --$quoted"
}

if $install_service; then
    step "Installing the $SERVICE service on $HOST"
    remote "$TARGET" "$SERVICE" "$SERVICE_USER" "$PORT" <<'REMOTE'
target="$1"; service="$2"; account="$3"; port="$4"

# a port below 1024 is privileged, and this service is deliberately not
capability=""
if [[ $port -lt 1024 ]]; then capability="AmbientCapabilities=CAP_NET_BIND_SERVICE"; fi

sudo=""; [[ $EUID -eq 0 ]] || sudo="sudo"

id -u "$account" >/dev/null 2>&1 || $sudo useradd --system --no-create-home --shell /usr/sbin/nologin "$account"

$sudo mkdir -p "$target"
# owned by whoever deploys, readable by the account that runs it: the app never writes to its own directory
$sudo chown -R "$(id -un):$account" "$target"

$sudo tee "/etc/systemd/system/$service.service" >/dev/null <<UNIT
[Unit]
Description=RMG, a random music generator
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$account
WorkingDirectory=$target
ExecStart=$target/Rmg.WebApi
Restart=on-failure
RestartSec=5
# the settings live in a file of their own that only root can read. A unit file is world readable,
# and the dashboard token has no business being in one; it also means changing a setting is a rewrite
# and a restart rather than a reinstall.
EnvironmentFile=/etc/$service/$service.env
$capability

# the analytics live here and nowhere else. StateDirectory is made, owned and kept by systemd, and
# survives a deploy replacing everything else; it is the one place the service is allowed to write.
StateDirectory=$service

# it serves static files and generates songs; it has no business anywhere else on the disk
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true

[Install]
WantedBy=multi-user.target
UNIT

$sudo systemctl daemon-reload
$sudo systemctl enable "$service" >/dev/null
echo "    $service installed, running as $account"
REMOTE
fi

# --- transfer --------------------------------------------------------------

step "Stopping $SERVICE"
remote "$SERVICE" <<'REMOTE'
service="$1"
sudo=""; [[ $EUID -eq 0 ]] || sudo="sudo"

if $sudo systemctl is-active --quiet "$service"; then
    $sudo systemctl stop "$service"
    echo "    stopped"
else
    echo "    was not running"
fi
REMOTE

# how long a superseded bundle folder stays on the server for pages that were open before the deploy
readonly ASSETS_KEPT_DAYS=7

step "Sending the app to $HOST:$TARGET"

# A page opened before this deploy still points at the bundle folder it was served with, and fetches
# the MP3 worker from it only when Export is pressed, so that folder has to outlive the deploy. The
# one the live pages point at is marked as retired now, and the prune below counts from that.
remote "$TARGET" <<'REMOTE'
target="$1"
if [[ -f $target/wwwroot/index.html ]]; then
    live="$(grep -o 'assets/[0-9a-f]\{8\}' "$target/wwwroot/index.html" | head -1 || true)"
    if [[ -n $live && -d $target/wwwroot/$live ]]; then touch "$target/wwwroot/$live"; fi
fi
REMOTE

# the bundles first and without --delete, so the pages that follow never point at a folder not there yet
rsync -az --human-readable \
    --chmod=D755,F644 \
    "$STAGE/wwwroot/assets/" "$HOST:$TARGET/wwwroot/assets/"

# --delete so a file dropped from the build goes too, but never into soundfonts: what is there is
# tens of megabytes the operator put there deliberately, and is not ours to remove. Nor into the
# bundles, which the prune below looks after.
rsync -az --delete --human-readable \
    --exclude "wwwroot/soundfonts/***" \
    --exclude "wwwroot/assets/***" \
    --chmod=D755,F644 \
    "$STAGE/" "$HOST:$TARGET/"

remote "$TARGET" "$SERVICE_USER" "$ASSETS_KEPT_DAYS" <<'REMOTE'
target="$1"; account="$2"; kept_days="$3"
mkdir -p "$target/wwwroot/soundfonts"
chmod +x "$target/Rmg.WebApi"

# bundle folders no page here points at any more, retired longer ago than a tab is likely to stay open
live="$(grep -oh 'assets/[0-9a-f]\{8\}' "$target"/wwwroot/*.html | sort -u || true)"
pruned=0
for folder in "$target"/wwwroot/assets/*/; do
    folder="${folder%/}"
    [[ -d $folder ]] || continue
    grep -qx "assets/${folder##*/}" <<< "$live" && continue
    if [[ -n $(find "$folder" -maxdepth 0 -mtime +"$kept_days") ]]; then rm -rf "$folder"; pruned=$((pruned + 1)); fi
done
echo "    bundle: $(head -1 <<< "$live"), $pruned old one(s) pruned"

# the native libraries beside it have to keep their bit too
find "$target" -maxdepth 1 -name '*.so' -exec chmod +x {} +
if id -u "$account" >/dev/null 2>&1; then
    sudo=""; [[ $EUID -eq 0 ]] || sudo="sudo"
    $sudo chgrp -R "$account" "$target"
    chmod -R g+rX "$target"
fi
echo "    sent"
REMOTE

# --- soundfont -------------------------------------------------------------

if $fetch_soundfont; then
    step "Fetching the soundfonts on the VPS"
    remote "$TARGET" "$SERVICE_USER" "$SOUNDFONTS" <<'REMOTE'
target="$1"; account="$2"; wanted="$3"
directory="$target/wwwroot/soundfonts"
mkdir -p "$directory"

get() {
    local url="${1%%|*}" name="$2"
    local destination="$directory/$name"

    if [[ -s $destination ]]; then
        echo "    $name is already there ($(du -h "$destination" | cut -f1))"
        return
    fi

    echo "    downloading $name…"

    # to a part file, so an interrupted download is never served or mistaken for a finished one
    local part="$destination.part"
    if ! curl -fsSL --retry 3 --connect-timeout 20 -o "$part" "$url"; then
        rm -f "$part"
        echo "    could not download $name from $url" >&2
        return 1
    fi

    # an archive is unpacked and the soundfont inside it is what takes the name
    if [[ $url == *.tar.gz || $url == *.tgz ]]; then
        local unpacked
        unpacked="$(mktemp -d)"

        if ! tar xzf "$part" -C "$unpacked"; then
            rm -rf "$part" "$unpacked"
            echo "    $name arrived as an archive that would not open" >&2
            return 1
        fi

        local found
        found="$(find "$unpacked" -type f \( -iname '*.sf2' -o -iname '*.sf3' -o -iname '*.sfogg' -o -iname '*.dls' \) | head -1)"

        if [[ -z $found ]]; then
            rm -rf "$part" "$unpacked"
            echo "    $name arrived as an archive with no soundfont in it" >&2
            return 1
        fi

        mv "$found" "$destination"
        rm -rf "$part" "$unpacked"
    else
        mv "$part" "$destination"
    fi

    echo "    $name ($(du -h "$destination" | cut -f1))"
}

# in the order given, which puts each licence before the soundfont it belongs to
while IFS= read -r line; do
    [[ -z $line ]] && continue

    url="${line%%|*}"
    name="${line#*|}"
    if [[ $name == "$line" ]]; then name="${url##*/}"; fi

    get "$line" "$name"
done <<< "$wanted"

# readable by the account that serves them; the directory stays writable, or the next deploy
# would have nowhere to put its .part file
if id -u "$account" >/dev/null 2>&1; then
    sudo=""; [[ $EUID -eq 0 ]] || sudo="sudo"
    $sudo chgrp -R "$account" "$directory"
fi
chmod -R g+rX "$directory"
REMOTE
fi
# --- settings --------------------------------------------------------------

step "Writing the settings on $HOST"
remote "$SERVICE" "$ADDRESS" "$PORT" "$KNOWN_PROXIES" "$DASHBOARD_TOKEN" "$RETENTION_DAYS" \
       "$DEFAULT_SOUNDFONT" "$ANALYTICS_ENABLED" <<'REMOTE'
service="$1"; address="$2"; port="$3"; proxies="$4"; dashboard="$5"; retention="$6"
soundfont="$7"; counting="$8"
sudo=""; [[ $EUID -eq 0 ]] || sudo="sudo"

$sudo install -d -m 700 -o root -g root "/etc/$service"

# Written whole every time, so a setting taken out of deploy.env is a setting taken off the server.
# Only what this deployment actually decides goes in: everything else is left to appsettings.json,
# which ships with the app. ASPNETCORE_ENVIRONMENT is not here because Production is already what a
# published app is without it, and the telemetry notice is not printed by one at all.
{
    echo "# Written by deploy.sh. Anything edited here goes on the next deploy."
    echo "# These override appsettings.json, which ships with the app and holds everything else."

    # the port is pinned rather than left to the default: it is what the proxy in front was told
    echo "RMG_PORT=$port"

    # a deployment listens on loopback whatever the shipped default says, since something proxies it
    echo "RMG_ADDRESS=$address"

    # systemd makes this and keeps it across deploys; the app has nowhere else it may write
    echo "RMG_STATEDIRECTORY=/var/lib/$service"

    if [[ -n $proxies ]]; then echo "RMG_KNOWNPROXIES=$proxies"; fi
    if [[ -n $dashboard ]]; then echo "RMG_DASHBOARDTOKEN=$dashboard"; fi
    if [[ -n $retention ]]; then echo "RMG_RETENTIONDAYS=$retention"; fi
    if [[ -n $soundfont ]]; then echo "RMG_DEFAULTSOUNDFONT=$soundfont"; fi
    if [[ -n $counting ]]; then echo "RMG_ANALYTICSENABLED=$counting"; fi
} | $sudo tee "/etc/$service/$service.env.new" >/dev/null

$sudo chown root:root "/etc/$service/$service.env.new"
$sudo chmod 600 "/etc/$service/$service.env.new"
$sudo mv "/etc/$service/$service.env.new" "/etc/$service/$service.env"

echo "    /etc/$service/$service.env, readable by root and nobody else"
REMOTE

# --- start -----------------------------------------------------------------

step "Starting $SERVICE"
remote "$SERVICE" "$PORT" <<'REMOTE'
service="$1"; port="$2"
sudo=""; [[ $EUID -eq 0 ]] || sudo="sudo"

$sudo systemctl start "$service"

# it answers as soon as Kestrel is up, which is a second or two after systemd calls it started
for _ in $(seq 1 20); do
    if curl -fsS --max-time 3 "http://127.0.0.1:$port/api/soundfonts" >/dev/null 2>&1; then
        echo "    up, and serving $(curl -fsS "http://127.0.0.1:$port/api/soundfonts" | grep -o '"file"' | wc -l) soundfont(s)"

        # every file the pages point at, which is what a mismatch between the pages and the bundles breaks
        for page in / /dashboard.html; do
            for file in $(curl -fsS "http://127.0.0.1:$port$page" | grep -o 'assets/[0-9a-f]\{8\}/[a-z0-9.-]*' | sort -u); do
                if ! curl -fsS -o /dev/null "http://127.0.0.1:$port/$file"; then
                    echo "    $page points at $file, which is not served" >&2
                    exit 1
                fi
            done
        done
        echo "    the pages and their bundles all answer"
        exit 0
    fi
    sleep 1
done

echo "    $service did not answer on port $port within 20s" >&2
$sudo systemctl --no-pager --lines 20 status "$service" >&2 || true
exit 1
REMOTE

step "Deployed to $HOST"
note "logs:    ssh $HOST 'journalctl -u $SERVICE -f'"
note "restart: ssh $HOST 'sudo systemctl restart $SERVICE'"

if [[ "$ADDRESS" == "loopback" ]]; then
    note "listening on loopback only, so put Caddy or another proxy in front of 127.0.0.1:$PORT."
    note "--address any to answer on every interface instead, which wants a firewall."
fi

if [[ -n "$DASHBOARD_TOKEN" ]]; then
    note "analytics: /dashboard.html, which asks for the token in RMG_DASHBOARDTOKEN."
else
    note "analytics are collected, but the dashboard is off: set RMG_DASHBOARDTOKEN to see them."
fi
