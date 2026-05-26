#!/bin/bash

# Exit immediately if a command exits with a non-zero status
set -e

# Global Variables
declare domain_name
declare email_address
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

    echo "..........[PREREQUISITES] Configuring UFW..." >&2

    ufw --force disable

    ufw default deny incoming
    ufw default allow outgoing

    # Allow SSH (adjust the port if you're using a non-standard SSH port)
    ufw allow 22/tcp

    # Caddy listens on these. 443/udp is for HTTP/3.
    ufw allow 80/tcp
    ufw allow 443/tcp
    ufw allow 443/udp

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

    # Add Docker's official GPG key into a dedicated keyring.
    # apt-key is deprecated and removed on newer Ubuntu releases (24.04+).
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc

    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list

    apt-get update

    # docker-compose-plugin is required - this script uses `docker compose` (v2).
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

    # SUDO_USER is empty when the script is run directly as root - skip then.
    if [ -n "$SUDO_USER" ]; then
        usermod -aG docker "$SUDO_USER"
    fi

    echo "..........[PREREQUISITES] Docker has been installed successfully." >&2
}

prerequisites_installation() {
    echo "
===============================
  1. PlikShare - prerequisites
===============================

The following components will be installed or configured:
- Docker (if not already installed)
- Uncomplicated Firewall (UFW) - opens 22, 80 and 443

These components are necessary for running PlikShare with Caddy as the
reverse proxy.
" >&2

    if ask_yes_no "Do you want to proceed with the prerequisites installation?"; then
        if ! command_exists docker; then
            echo "..........[PREREQUISITES] Installing Docker..."
            install_docker
        else
            echo "..........[PREREQUISITES] Docker is already installed."
        fi

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
" >&2

    # Domain name
    echo "
Domain Name:
    Your domain name is used to configure your PlikShare instance and for
    Caddy to obtain a TLS certificate from Let's Encrypt.

    IMPORTANT: Before proceeding, ensure that your domain is already configured
    to point to this machine's IP address. Caddy uses HTTP-01 challenge by
    default and needs port 80 reachable on this domain.

    - Configure 'A' record for a selected domain/subdomain which will point
      to this machine's IP address.

    DNS changes may take some time to propagate, so you might need to wait
    before proceeding.

    NOTE: Enter your domain name without 'http://' or 'https://'
    (e.g., example.com, not https://example.com).
    " >&2
    while true; do
        read -p "Enter your domain name: " domain_name
        if [[ $domain_name =~ ^([a-zA-Z0-9]([-a-zA-Z0-9]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$ ]]; then
            break
        else
            echo "[ERROR] Invalid domain name. Please enter a valid domain (e.g., example.com, sub.example.com)." >&2
        fi
    done

    PLIKSHARE_APP_URL="https://$domain_name"

    # Email address
    echo "
Email Address:
    Please enter your email address. This email will be used for:
    1. Let's Encrypt notifications about your TLS certificate (Caddy passes
       this in the ACME registration).
    2. The super-admin account for your PlikShare instance.

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

    Future password rotation:
    - In the future, you can add new passwords by appending them with commas.
    - This allows for password rotation without immediate data re-encryption.
    - Example of future format: oldpassword,newpassword1,newpassword2

    IMPORTANT:
    - Never remove old passwords from this list, only add new ones.
    - Removing a password may result in data loss.
    - Keep these passwords secure and don't lose them!

    For now, please enter only one password:
    " >&2
    ask_silent_input "PLIKSHARE_ENCRYPTION_PASSWORDS" PLIKSHARE_ENCRYPTION_PASSWORDS true

    echo "
Plikshare Main Volume Path:
    This is the main storage location for your PlikShare data on the host system.
    It should be a path where you want to store all PlikShare-related data - like
    the SQLite database files.
    Default is './plikshare_data' in the current directory.
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
    You can configure additional volumes for PlikShare to use.
    These volumes are used to create 'Hard Drive' type storage in PlikShare.

    Use cases for additional volumes:
    1. Mount external storage:
       - Cloud provider volumes (e.g., DigitalOcean volumes, AWS EBS)

    2. Utilize different local directories:
       - Separate SSDs or HDDs for performance or capacity reasons
       - Specific directories for organizational purposes

    For each additional volume, you'll need to provide:
    - The path on your host system (e.g., /mnt/external_volume, /data/plikshare_files)
    - A name for the volume as it will appear in PlikShare

    You can add multiple volumes. We'll prompt you for each one separately.
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

# Writes install-time configuration (including secrets) to plikshare.env.
# docker-compose.yml references this file via `env_file:`, so Docker Compose
# reads the values from disk every time it (re)creates the plikshare container.
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
    expose:
      - "8080"
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

  caddy:
    image: caddy:2-alpine
    restart: always
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    depends_on:
      - plikshare

volumes:
  caddy_data:
  caddy_config:
EOF

    echo "..........[INSTALLATION] Docker Compose file generated." >&2
}

# Writes the Caddyfile. Caddy handles ACME on its own — no two-phase nginx
# bootstrap, no Certbot, no manual cert swap. On first `docker compose up`,
# Caddy requests a Let's Encrypt cert via HTTP-01 challenge on port 80 and
# starts serving HTTPS once it has one.
write_caddyfile() {
    echo "..........[INSTALLATION] Writing Caddyfile..." >&2

    cat > Caddyfile <<EOF
{
    email $email_address
}

$domain_name {
    encode gzip

    reverse_proxy plikshare:8080

    request_body {
        max_size 100MB
    }

    header {
        Strict-Transport-Security "max-age=31536000"
        X-Content-Type-Options "nosniff"
        Referrer-Policy "strict-origin-when-cross-origin"
    }
}
EOF

    echo "..........[INSTALLATION] Caddyfile generated." >&2
}

start_caddy_stack() {
    echo "..........[INSTALLATION] Starting PlikShare and Caddy..." >&2
    docker compose up -d

    echo "..........[INSTALLATION] Caddy is now requesting a TLS certificate from Let's Encrypt." >&2
    echo "..........[INSTALLATION] This usually takes 10-60 seconds. You can watch progress with:" >&2
    echo "                docker compose logs -f caddy" >&2

    echo "
===================================
  PlikShare Installation Complete
===================================

Your PlikShare service is now configured with HTTPS support.
- PlikShare is running and accessible via HTTPS
- HTTP requests will automatically redirect to HTTPS
- Caddy will automatically renew certificates before they expire
- HTTP/2 and HTTP/3 are enabled by default
- gzip compression is on

You can now access your PlikShare instance securely at: https://$domain_name
" >&2
}

install_plikshare() {
    echo "
===============================
  3. PlikShare - Installation
===============================

We are now ready to install PlikShare on your server. This process involves
three steps:

1. Generating a Docker Compose file tailored to your configuration.
2. Writing a Caddyfile that handles HTTPS automatically.
3. Starting the stack — Caddy requests a Let's Encrypt certificate on first boot.

Unlike a Nginx + Certbot setup, there is no two-phase bootstrap and no
manual certificate management. Caddy fetches and renews certs on its own.
" >&2

    if ! ask_yes_no "Do you want to proceed with the PlikShare installation?" "..........[INSTALLATION] Proceeding with installation..." "..........[INSTALLATION] Installation aborted by user."; then
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

    if ! write_caddyfile; then
        echo "[ERROR] Installation aborted due to Caddyfile generation failure." >&2
        return 1
    fi

    if ! start_caddy_stack; then
        echo "[ERROR] Installation aborted due to Caddy stack startup failure." >&2
        return 1
    fi

    return 0
}

setup_cron_jobs() {
    echo "
===================================
  4. PlikShare - Cron Jobs Setup
===================================

Caddy handles TLS certificate renewal automatically — no cron job is needed
for that. You can optionally schedule nightly PlikShare updates.
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
  Reverse proxy: Caddy
===============================

Welcome to the PlikShare installation process.
This script sets up PlikShare with Caddy as the reverse proxy.
Caddy handles HTTPS automatically — no Certbot, no two-phase bootstrap.
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
