#!/bin/bash

# Exit immediately if a command exits with a non-zero status
set -e

# Global Variables
declare email_address
declare TAILSCALE_AUTH_KEY
declare TAILNET_HOSTNAME
declare -a additional_volumes

# Utils Functions
ask_yes_no() {
    local prompt="$1"
    local yes_message="${2}"
    local no_message="${3:-Aborting...}"

    while true; do
        read -p "$prompt (y/n): " response
        case $response in
            [Yy]* )
                echo "" >&2
                echo "$yes_message" >&2
                return 0
                ;;
            [Nn]* )
                echo "" >&2
                echo "$no_message" >&2
                return 1
                ;;
            * )
                echo "" >&2
                echo "Please answer [y]es or [n]o." >&2
                ;;
        esac
    done
}

ask_silent_input() {
    local prompt="$1"
    local var_name="$2"
    local confirm="${3:-false}"
    local check_password_strength="${4:-false}"
    local value
    local value_confirm

    while true; do
        read -s -p "Enter $prompt: " value
        echo >&2

        if [ "$check_password_strength" = true ]; then
            if ! [[ ${#value} -ge 8 && "$value" =~ [a-z] && "$value" =~ [A-Z] && "$value" =~ [0-9] && "$value" =~ [^a-zA-Z0-9] ]]; then
                echo >&2
                echo "[ERROR] Password must contain at least one lowercase and uppercase letter, one number and one special character, and be at least 8 characters long. Please try again." >&2
                echo >&2
                continue
            fi
        fi

        if [ "$confirm" = true ]; then
            read -s -p "Confirm $prompt: " value_confirm
            echo >&2

            if [ "$value" = "$value_confirm" ]; then
                break
            else
                echo >&2
                echo "[ERROR] Inputs do not match. Please try again." >&2
                echo >&2
            fi
        else
            break
        fi
    done

    eval "$var_name='$value'"
}

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to check if running as root
is_root() {
    return $(id -u)
}

# UFW for the Tailscale setup ONLY opens SSH on the public interface.
# PlikShare is reachable solely over the tailnet (tailscale0); no public
# ingress is required.
install_configure_ufw() {
    echo "..........[PREREQUISITES] Checking UFW installation..." >&2
    if ! command_exists ufw; then
        echo "..........[PREREQUISITES] UFW is not installed. Installing UFW..." >&2
        if ! is_root; then
            echo "[ERROR] This script needs root privileges to install UFW. Please run with sudo." >&2
            exit 1
        fi

        apt-get update
        apt-get install -y ufw

        echo "..........[PREREQUISITES] UFW has been installed successfully." >&2
    else
        echo "..........[PREREQUISITES] UFW is already installed." >&2
    fi

    echo "..........[PREREQUISITES] Configuring UFW (Tailscale mode — tailnet only)..." >&2

    ufw --force disable

    ufw default deny incoming
    ufw default allow outgoing

    # Allow SSH (adjust the port if you're using a non-standard SSH port)
    ufw allow 22/tcp

    # Allow all traffic on the tailnet interface — Tailscale ACLs decide who
    # gets through, not UFW. Without this UFW would block PlikShare.
    ufw allow in on tailscale0

    # No 80/443 — PlikShare is never exposed on the public interface.

    ufw logging low

    echo "y" | ufw enable

    echo "..........[PREREQUISITES] UFW has been configured and enabled." >&2
}

# Function for directory creation and permission setting
create_directory_and_set_permissions() {
    local dir_path="$1"

    echo "" >&2

    if [ ! -d "$dir_path" ]; then
        echo "..........[SETUP] Directory $dir_path does not exist. Creating it now..." >&2
        mkdir -p "$dir_path"
        if [ $? -eq 0 ]; then
            echo "..........[SETUP] Directory $dir_path created successfully." >&2
        else
            echo "[ERROR] Failed to create directory $dir_path. Please check your permissions and try again." >&2
            return 1
        fi
    else
        echo "..........[SETUP] Directory $dir_path already exists." >&2
    fi

    echo "..........[SETUP] Setting permissions on ${dir_path}..." >&2
    sudo chown -R 5678:5678 ${dir_path}
    sudo chmod -R 755 ${dir_path}
    echo "..........[SETUP] Permissions on ${dir_path} set successfully" >&2
    return 0
}

# Function to install Docker
install_docker() {
    echo "..........[PREREQUISITES] Docker is not installed. Installing Docker..." >&2
    if ! is_root; then
        echo "[ERROR] This script needs root privileges to install Docker. Please run with sudo." >&2
        exit 1
    fi

    apt-get update

    apt-get install -y ca-certificates curl

    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc

    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list

    apt-get update

    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

    if [ -n "$SUDO_USER" ]; then
        usermod -aG docker "$SUDO_USER"
    fi

    echo "..........[PREREQUISITES] Docker has been installed successfully." >&2
}

# Installs the Tailscale daemon on the host (NOT in a container). Tailscale's
# official installer handles distro detection and the systemd unit. Running
# tailscaled on the host (vs in a container) keeps `tailscale serve` and
# MagicDNS working without extra capability juggling.
install_tailscale() {
    echo "..........[PREREQUISITES] Installing Tailscale..." >&2
    if ! is_root; then
        echo "[ERROR] This script needs root privileges to install Tailscale. Please run with sudo." >&2
        exit 1
    fi

    if command_exists tailscale; then
        echo "..........[PREREQUISITES] Tailscale is already installed." >&2
        return 0
    fi

    curl -fsSL https://tailscale.com/install.sh | sh

    echo "..........[PREREQUISITES] Tailscale installed." >&2
}

prerequisites_installation() {
    echo "
===============================
  1. PlikShare - prerequisites
===============================

The following components will be installed or configured:
- Docker (if not already installed)
- Tailscale daemon (if not already installed)
- Uncomplicated Firewall (UFW) - opens 22 only, plus all traffic on tailscale0

Tailscale mode does NOT open ports 80 / 443. PlikShare is reachable only
inside your tailnet.
" >&2

    if ask_yes_no "Do you want to proceed with the prerequisites installation?"; then
        if ! command_exists docker; then
            echo "..........[PREREQUISITES] Installing Docker..."
            install_docker
        else
            echo "..........[PREREQUISITES] Docker is already installed."
        fi

        echo "..........[PREREQUISITES] Installing Tailscale..."
        install_tailscale

        echo "..........[PREREQUISITES] Installing and configuring UFW..."
        install_configure_ufw

        echo "..........[PREREQUISITES] Prerequisites installation completed successfully."
        return 0
    else
        echo "[ERROR] Prerequisites installation aborted. PlikShare requires these components to function properly."
        return 1
    fi
}

prompt_for_setup_details() {
    echo "
===============================
  2. PlikShare - Setup Details
===============================

We'll now collect some important details for your PlikShare installation.

Before continuing, you should have:
- A Tailscale account (free for personal use — sign up at tailscale.com).
- An auth key from Tailscale: Settings → Keys → Generate auth key.
  Pre-authorized + tagged is recommended so this server joins automatically
  without a manual approval step.

No domain name and no DNS records are needed — Tailscale's MagicDNS gives
this server a hostname inside your tailnet automatically.
" >&2

    # Email address (admin account only — no LE/ACME registration here)
    echo "
Email Address:
    The super-admin account for your PlikShare instance.
    Make sure to provide a valid email address that you actively monitor.
    " >&2

    while true; do
        read -p "Enter your email address: " email_address
        if [[ $email_address =~ ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]]; then
            break
        else
            echo "[ERROR] Invalid email address. Please enter a valid email." >&2
        fi
    done

    echo "
Admin Initial Password:
    This is the initial password for your PlikShare admin account.
    You should change it after the first login for security reasons.
    Make sure to choose a strong, unique password.
    " >&2
    ask_silent_input "PLIKSHARE_APP_OWNERS_INITIAL_PASSWORD" PLIKSHARE_APP_OWNERS_INITIAL_PASSWORD true true

    echo "
PlikShare Encryption Passwords:
    A list of passwords which are used to encrypt sensitive information in
    the database at rest. It's a crucial security feature of PlikShare.

    Initial setup:
    - Start with only one password.
    - Choose a strong, unique password that you haven't used elsewhere.

    IMPORTANT:
    - Never remove old passwords from this list, only add new ones.
    - Removing a password may result in data loss.
    - Keep these passwords secure and don't lose them!

    For now, please enter only one password:
    " >&2
    ask_silent_input "PLIKSHARE_ENCRYPTION_PASSWORDS" PLIKSHARE_ENCRYPTION_PASSWORDS true

    echo "
Tailscale Auth Key:
    The auth key generated in your Tailscale admin console.
    It looks like 'tskey-auth-...'. This is used once to join this server
    to your tailnet, then discarded.
    " >&2
    ask_silent_input "Tailscale auth key" TAILSCALE_AUTH_KEY

    echo "
Plikshare Main Volume Path:
    This is the main storage location for your PlikShare data on the host system.
    It holds the SQLite database. Default is './plikshare_data'.
    " >&2
    read -p "Enter Plikshare Main Volume Path (press Enter for default): " PLIKSHARE_MAIN_VOLUME_PATH
    PLIKSHARE_MAIN_VOLUME_PATH=${PLIKSHARE_MAIN_VOLUME_PATH:-./plikshare_data}

    if ! create_directory_and_set_permissions "$PLIKSHARE_MAIN_VOLUME_PATH"; then
        echo "[ERROR] Failed to create or set permissions for the main volume path. Aborting setup."
        return 1
    fi

    echo "" >&2
    echo "..........[SETUP] PLIKSHARE_MAIN_VOLUME_PATH is set to: $PLIKSHARE_MAIN_VOLUME_PATH" >&2

    echo "
Additional Volumes:
    Optional volumes for the 'Hard Drive' storage type in PlikShare.

    For each additional volume, you'll need to provide:
    - The path on your host system
    - A name for the volume as it will appear in PlikShare
    " >&2
    mapfile -t additional_volumes < <(prompt_for_additional_volumes)

    echo "..........[SETUP] Setup details collected successfully." >&2
    return 0
}

prompt_for_additional_volumes() {
    local volumes=()

    echo "" >&2

    while true; do
        read -p "Do you want to add another volume? (y/n): " add_volume
        if [[ ! $add_volume =~ ^[Yy]$ ]]; then
            break
        fi

        read -p "Enter the path for the volume: " volume_path
        read -p "Enter the name for the volume: " volume_name

        if create_directory_and_set_permissions "$volume_path"; then
            volumes+=("$volume_path:$volume_name")
            echo "..........[SETUP] Volume $volume_path:$volume_name added successfully." >&2
            echo "" >&2
        else
            echo "[ERROR] Failed to create or set permissions for $volume_path. Skipping this volume." >&2
        fi
    done

    if [ ${#volumes[@]} -gt 0 ]; then
        printf '%s\n' "${volumes[@]}"
    fi
}

# Joins this machine to the tailnet using the auth key, then queries
# tailscaled for the MagicDNS hostname it ended up with. Without --ssh the
# join still works; we add it because admining a tailnet-only server over
# Tailscale SSH is far less awkward than juggling a bastion.
authenticate_tailscale() {
    echo "..........[TAILSCALE] Authenticating with Tailscale..." >&2

    tailscale up \
        --authkey="$TAILSCALE_AUTH_KEY" \
        --ssh \
        --accept-routes

    echo "..........[TAILSCALE] Waiting for tailnet to come up..." >&2
    local attempts=0
    while [ $attempts -lt 30 ]; do
        if tailscale status >/dev/null 2>&1; then
            break
        fi
        sleep 1
        attempts=$((attempts + 1))
    done

    # Pull the FQDN (e.g. 'fileserver.tail-abcd.ts.net.') from tailscale
    # status. Trim the trailing dot.
    TAILNET_HOSTNAME=$(tailscale status --json 2>/dev/null \
        | grep -oP '"DNSName"\s*:\s*"\K[^"]+' \
        | head -1 \
        | sed 's/\.$//')

    if [ -z "$TAILNET_HOSTNAME" ]; then
        echo "[ERROR] Could not determine tailnet hostname. Is Tailscale running?" >&2
        return 1
    fi

    PLIKSHARE_APP_URL="https://$TAILNET_HOSTNAME"
    echo "..........[TAILSCALE] Tailnet hostname: $TAILNET_HOSTNAME" >&2
    echo "..........[TAILSCALE] PlikShare will be reachable at: $PLIKSHARE_APP_URL" >&2
    return 0
}

generate_env_file() {
    echo "..........[INSTALLATION] Generating plikshare.env file..." >&2

    escape_env_value() {
        local v="$1"
        v="${v//\\/\\\\}"
        v="${v//\"/\\\"}"
        printf '%s' "$v"
    }

    cat > plikshare.env <<EOF
PlikShare_AppUrl="$(escape_env_value "$PLIKSHARE_APP_URL")"
PlikShare_AppOwners="$(escape_env_value "$email_address")"
PlikShare_AppOwnersInitialPassword="$(escape_env_value "$PLIKSHARE_APP_OWNERS_INITIAL_PASSWORD")"
PlikShare_EncryptionPasswords="$(escape_env_value "$PLIKSHARE_ENCRYPTION_PASSWORDS")"
EOF

    chmod 600 plikshare.env

    echo "..........[INSTALLATION] plikshare.env file generated." >&2
}

generate_docker_compose() {
    echo "..........[INSTALLATION] Generating Docker Compose file..." >&2

    # Bind to 127.0.0.1 only — tailscale serve (on the host) is what fronts
    # this on the tailnet. Without the 127.0.0.1 prefix, Docker would publish
    # on 0.0.0.0 and the port would be reachable on any interface UFW lets
    # through. UFW does block public 8080, but defense in depth.
    cat > docker-compose.yml <<EOF
services:
  plikshare:
    image: damiankrychowski/plikshare:latest
    restart: always
    env_file:
      - ./plikshare.env
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - PlikShare_Volumes__Path=volumes
      - PlikShare_Volumes__Main__Path=main
EOF

    if [ ${#additional_volumes[@]} -gt 0 ]; then
        for i in "${!additional_volumes[@]}"; do
            IFS=':' read -r path name <<< "${additional_volumes[$i]}"
            echo "      - PlikShare_Volumes__Other__${i}__Path=$name" >> docker-compose.yml
        done
    fi

    cat >> docker-compose.yml <<EOF
    ports:
      - "127.0.0.1:8080:8080"
    volumes:
      - ${PLIKSHARE_MAIN_VOLUME_PATH}:/app/volumes/main:rw
EOF

    if [ ${#additional_volumes[@]} -gt 0 ]; then
        for volume in "${additional_volumes[@]}"; do
            IFS=':' read -r path name <<< "$volume"
            echo "      - $path:/app/volumes/$name:rw" >> docker-compose.yml
        done
    fi

    cat >> docker-compose.yml <<EOF
    user: "5678:5678"
EOF

    echo "..........[INSTALLATION] Docker Compose file generated." >&2
}

# Starts plikshare locally and then fronts it on the tailnet over HTTPS via
# `tailscale serve`. Tailscale issues the *.ts.net cert automatically.
start_stack() {
    echo "..........[INSTALLATION] Starting PlikShare..." >&2
    docker compose up -d

    echo "..........[INSTALLATION] Waiting for PlikShare to come online..." >&2
    local attempts=0
    while [ $attempts -lt 30 ]; do
        if curl -fsS --max-time 2 http://127.0.0.1:8080/ >/dev/null 2>&1 \
            || curl -fsS --max-time 2 -o /dev/null -w '' http://127.0.0.1:8080/ >/dev/null 2>&1; then
            break
        fi
        sleep 1
        attempts=$((attempts + 1))
    done

    echo "..........[INSTALLATION] Exposing PlikShare on the tailnet via Tailscale Serve..." >&2
    # Background mode persists across reboots — Tailscale stores the serve
    # config in its state file.
    tailscale serve --bg --https=443 http://127.0.0.1:8080

    cat >&2 <<EOF

===================================
  PlikShare Installation Complete
===================================

Your PlikShare service is now configured for tailnet-only access.

  Reachable at: $PLIKSHARE_APP_URL

- Devices on your tailnet (and with permission) can sign in.
- Devices outside your tailnet cannot reach this server.
- Tailscale handles HTTPS certificates automatically.
- No public ports are open to the internet.

To invite more people: add their account to your tailnet from the
Tailscale admin console (https://login.tailscale.com/admin), then have
them install the Tailscale client on their device.

REMINDER: public PlikShare share links won't work for people who are NOT
on your tailnet. That's the whole point of this deployment mode — if you
need that, redeploy with the Caddy, Nginx or Cloudflare Tunnel script.
EOF
}

install_plikshare() {
    echo "
===============================
  3. PlikShare - Installation
===============================

We are now ready to install PlikShare. This process involves four steps:

1. Joining this machine to your tailnet with the auth key.
2. Generating plikshare.env (chmod 600).
3. Generating a Docker Compose file (PlikShare bound to 127.0.0.1 only).
4. Starting PlikShare and exposing it on the tailnet via Tailscale Serve.
" >&2

    if ! ask_yes_no "Do you want to proceed with the PlikShare installation?" "..........[INSTALLATION] Proceeding with installation..." "..........[INSTALLATION] Installation aborted by user."; then
        return 1
    fi

    if ! authenticate_tailscale; then
        echo "[ERROR] Installation aborted due to Tailscale authentication failure." >&2
        return 1
    fi

    if ! generate_env_file; then
        echo "[ERROR] Installation aborted due to env file generation failure." >&2
        return 1
    fi

    if ! generate_docker_compose; then
        echo "[ERROR] Installation aborted due to Docker Compose file generation failure." >&2
        return 1
    fi

    if ! start_stack; then
        echo "[ERROR] Installation aborted due to stack startup failure." >&2
        return 1
    fi

    return 0
}

setup_cron_jobs() {
    echo "
===================================
  4. PlikShare - Cron Jobs Setup
===================================

Tailscale handles certificates for *.ts.net automatically — no renewal cron
is needed. You can optionally schedule nightly PlikShare updates.
" >&2

    remove_existing_cron_jobs() {
        echo "..........[CRON] Removing existing PlikShare-related cron jobs..." >&2
        crontab -l 2>/dev/null | grep -v "update_plikshare.sh" | crontab -
    }

    add_plikshare_update_cron_job() {
        echo "..........[CRON] Setting up nightly update cron job for PlikShare..." >&2
        update_script="$PWD/update_plikshare.sh"
        cat > "$update_script" <<EOF
#!/bin/bash
cd $(pwd)
docker compose pull plikshare
docker compose up -d --no-deps plikshare
EOF
        chmod +x "$update_script"

        (crontab -l 2>/dev/null; echo "0 2 * * * $update_script") | crontab -
        echo "..........[CRON] Cron job for nightly updates has been set up. Updates will run daily at 2 AM." >&2
    }

    remove_existing_cron_jobs

    echo ""

    if ask_yes_no "Do you want to schedule nightly updates for PlikShare?" "..........[CRON] Nightly updates will be scheduled." "..........[CRON] Nightly updates will not be scheduled."; then
        add_plikshare_update_cron_job
    else
        echo "You can manually update PlikShare when needed using the following commands:
        cd $(pwd)
        docker compose pull plikshare
        docker compose up -d --no-deps plikshare" >&2
    fi

    echo "..........[CRON] Cron jobs setup completed." >&2
    return 0
}

main_installation() {
    echo "
===============================
  PlikShare Installation Setup
  Network: Tailscale (private)
===============================

Welcome to the PlikShare installation process.
This script sets up PlikShare for tailnet-only access via Tailscale.

WARNING: public share links sent to people who are NOT on your tailnet
will not work. This deployment is intentionally private. If you need to
share files with anyone with a browser, pick a different install script
(Caddy / Nginx / Cloudflare Tunnel).
" >&2
    if ! prerequisites_installation; then
        echo "[ERROR] Installation aborted due to prerequisites installation failure." >&2
        return 1
    fi

    if ! prompt_for_setup_details; then
        echo "[ERROR] Installation aborted due to setup details collection failure." >&2
        return 1
    fi

    if ! install_plikshare; then
        return 1
    fi

    if ! setup_cron_jobs; then
        echo "[ERROR] Installation aborted due to cron jobs setup failure." >&2
        return 1
    fi

    echo "
=====================================
  !!! PlikShare Is Ready To Use !!!
=====================================
" >&2

    return 0
}

if ! main_installation; then
    exit 1
fi
