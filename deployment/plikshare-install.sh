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

    # Use eval to assign the value to the variable name passed as argument
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

        # Install UFW
        apt-get update
        apt-get install -y ufw

        echo "..........[PREREQUISITES] UFW has been installed successfully." >&2
    else
        echo "..........[PREREQUISITES] UFW is already installed." >&2
    fi

    # Configure UFW
    echo "..........[PREREQUISITES] Configuring UFW..." >&2
    
    # Disable UFW to start with a clean slate
    ufw --force disable

    # Set default policies
    ufw default deny incoming
    ufw default allow outgoing

    # Allow SSH (adjust the port if you're using a non-standard SSH port)
    ufw allow 22/tcp

    # Allow HTTP and HTTPS
    ufw allow 80/tcp
    ufw allow 443/tcp
    
    # Adjust logging (options: on, off, low, medium, high)
    ufw logging low

    # Enable UFW
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

set_permanent_env_variable() {
    local var_name="$1"
    local var_value="$2"
    
    # Check if the variable already exists in /etc/environment
    if grep -q "^${var_name}=" /etc/environment; then
        # If it exists, update it
        sudo sed -i "s|^${var_name}=.*|${var_name}=\"${var_value}\"|" /etc/environment
    else
        # If it doesn't exist, append it
        echo "${var_name}=\"${var_value}\"" | sudo tee -a /etc/environment > /dev/null
    fi
}

# Function to install Docker
install_docker() {
    echo "..........[PREREQUISITES] Docker is not installed. Installing Docker..." >&2
    if ! is_root; then
        echo "[ERROR] This script needs root privileges to install Docker. Please run with sudo." >&2
        exit 1
    fi

    # Update package index
    apt-get update

    # Install packages to allow apt to use a repository over HTTPS
    apt-get install -y apt-transport-https ca-certificates curl software-properties-common

    # Add Docker's official GPG key
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | apt-key add -

    # Set up the stable repository
    add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"

    # Update the package index again
    apt-get update

    # Install the latest version of Docker CE
    apt-get install -y docker-ce

    # Allow current user to run Docker commands without sudo
    usermod -aG docker $SUDO_USER

    echo "..........[PREREQUISITES] Docker has been installed successfully." >&2
}

prerequisites_installation() {
    echo "
===============================
  1. PlikShare - prerequisites
===============================

The following components will be installed or configured:
- Docker (if not already installed)
- Uncomplicated Firewall (UFW)

These components are necessary for running PlikShare securely.
" >&2

    if ask_yes_no "Do you want to proceed with the prerequisites installation?"; then
        # Install Docker if not present
        if ! command_exists docker; then
            echo "..........[PREREQUISITES] Installing Docker..."
            install_docker
        else
            echo "..........[PREREQUISITES] Docker is already installed."
        fi

        # Install and configure UFW
        echo "..........[PREREQUISITES] Installing and configuring UFW..."
        install_configure_ufw

        # The script will continue with Nginx and Certbot setup later

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
    Your domain name is used to configure your PlikShare instance and set up SSL certificates.
    It should be a valid domain that you own and control (e.g., example.com, plikshare.yourdomain.com).

    IMPORTANT: Before proceeding, ensure that your domain is already configured to point to this machine's IP address.
    This is crucial for the SSL certificate acquisition process (ACME challenge) to work correctly.
    
    - Configure 'A' record for a selected domain/subdomain which will point to this machine's IP address.

    If your DNS is not yet configured, please do so now before entering your domain name.
    DNS changes may take some time to propagate, so you might need to wait before proceeding.

    NOTE: Please enter your domain name without 'http://' or 'https://' (e.g., example.com, not https://example.com).
    " >&2
    while true; do
        read -p "Enter your domain name: " domain_name
        if [[ $domain_name =~ ^([a-zA-Z0-9]([-a-zA-Z0-9]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$ ]]; then
            break
        else
            echo "[ERROR] Invalid domain name. Please enter a valid domain (e.g., example.com, sub.example.com)." >&2
        fi
    done
    
    # Set PLIKSHARE_APP_URL based on the domain
    PLIKSHARE_APP_URL="https://$domain_name"
    export PlikShare_AppUrl="${PLIKSHARE_APP_URL}"
    set_permanent_env_variable "PlikShare_AppUrl" "$PLIKSHARE_APP_URL"  

    # Email address
    echo "
Email Address:
    Please enter your email address. This email will be used for:
    1. Important notifications about your SSL certificate
    2. The super-admin account for your PlikShare instance

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
    
    export PlikShare_AppOwners="${email_address}"
    set_permanent_env_variable "PlikShare_AppOwners" "$email_address"  

    # PLIKSHARE_APP_OWNERS_INITIAL_PASSWORD
    echo "
Admin Initial Password:
    This is the initial password for your PlikShare admin account. 
    You should change it after the first login for security reasons.
    Make sure to choose a strong, unique password.
    " >&2
    ask_silent_input "PLIKSHARE_APP_OWNERS_INITIAL_PASSWORD" PLIKSHARE_APP_OWNERS_INITIAL_PASSWORD true true
    
    # Set and export the password
    export PlikShare_AppOwnersInitialPassword="${PLIKSHARE_APP_OWNERS_INITIAL_PASSWORD}"
    set_permanent_env_variable "PlikShare_AppOwnersInitialPassword" "$PLIKSHARE_APP_OWNERS_INITIAL_PASSWORD"    

    # PLIKSHARE_ENCRYPTION_PASSWORDS
    echo "
PlikShare Encryption Passwords:
    A list of passwords which are used to encrypt sensitive information in the database at rest.
    It's a crucial security feature of PlikShare. 

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

    # Set and export the encryption passwords
    export PlikShare_EncryptionPasswords="${PLIKSHARE_ENCRYPTION_PASSWORDS}"
    set_permanent_env_variable "PlikShare_EncryptionPasswords" "$PLIKSHARE_ENCRYPTION_PASSWORDS"


    # PLIKSHARE_MAIN_VOLUME_PATH
    echo "
Plikshare Main Volume Path:
    This is the main storage location for your PlikShare data on the host system.
    It should be a path where you want to store all PlikShare-related data - like the SQLite database files.
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

    Benefits:
    - Scalability: Easily expand storage capacity
    - Flexibility: Use different storage types for different purposes
    - Integration: Connect PlikShare with your existing storage infrastructure

    For each additional volume, you'll need to provide:
    - The path on your host system (e.g., /mnt/external_volume, /data/plikshare_files)
    - A name for the volume as it will appear in PlikShare

    You can add multiple volumes. We'll prompt you for each one separately.
    " >&2
    mapfile -t additional_volumes < <(prompt_for_additional_volumes)

    echo "..........[SETUP] Setup details collected successfully." >&2
    return 0
}

# Function to prompt for additional volumes
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

generate_docker_compose() {
    echo "..........[INSTALLATION] Generating Docker Compose file..." >&2

    # Start generating Docker Compose file
    cat > docker-compose.yml <<EOF
services:
  plikshare:
    image: damiankrychowski/plikshare:latest
    restart: always
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - PlikShare_AppUrl
      - PlikShare_AppOwners
      - PlikShare_AppOwnersInitialPassword
      - PlikShare_EncryptionPasswords
      - PlikShare_Volumes__Path=volumes
      - PlikShare_Volumes__Main__Path=main
EOF

    # Add additional volumes to the environment variables
    if [ ${#additional_volumes[@]} -gt 0 ]; then
        for i in "${!additional_volumes[@]}"; do
            IFS=':' read -r path name <<< "${additional_volumes[$i]}"
            echo "      - PlikShare_Volumes__Other__${i}__Path=$name" >> docker-compose.yml
        done
    fi

    # Continue with the rest of the Docker Compose file
    cat >> docker-compose.yml <<EOF
    expose:
      - "8080"
    volumes:
      - ${PLIKSHARE_MAIN_VOLUME_PATH}:/app/volumes/main:rw
EOF

    # Add additional volumes to the volumes section
    if [ ${#additional_volumes[@]} -gt 0 ]; then
        for volume in "${additional_volumes[@]}"; do
            IFS=':' read -r path name <<< "$volume"
            echo "      - $path:/app/volumes/$name:rw" >> docker-compose.yml
        done
    fi

    # Finish the Docker Compose file
    cat >> docker-compose.yml <<EOF
    user: "5678:5678"

  nginx:
    image: nginx:latest
    restart: always
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./certbot/www:/var/www/certbot/:ro
      - ./certbot/conf/:/etc/nginx/ssl/:ro
    depends_on:
      - plikshare

  certbot:
    image: certbot/certbot:latest
    volumes:
      - ./certbot/www/:/var/www/certbot/:rw
      - ./certbot/conf/:/etc/letsencrypt/:rw
EOF

    echo "..........[INSTALLATION] Docker Compose file generated." >&2
}

setup_basic_nginx_for_ssl_setup() {    
    # Function to check if a container is running
    is_container_running() {
        docker compose ps --services --filter "status=running" | grep -q "$1"
    }
    
    # Function to wait for a container to be running
    wait_for_container() {
        echo "..........[INSTALLATION] Waiting for $1 container to start..." >&2
        while ! is_container_running "$1"; do
            echo "..........[INSTALLATION] Still waiting for $1... This may take a few moments." >&2
            sleep 5
        done
        echo "..........[INSTALLATION] $1 container is now running." >&2
    }
  
    echo "..........[INSTALLATION] Setting up temporary Nginx configuration for SSL certificate acquisition..." >&2
    echo "..........[INSTALLATION] This step prepares Nginx to respond to the ACME challenge from Let's Encrypt." >&2

    # Generate initial nginx configuration
    cat > nginx-http.conf <<EOF
events {
    worker_connections 1024;
}

http {
    server {
        listen 80;
        server_name $domain_name;

        location /.well-known/acme-challenge/ {
            root /var/www/certbot;
        }

        location / {
            return 404;
        }
    }
}
EOF

    echo "..........[INSTALLATION] Temporary Nginx configuration created." >&2
    echo "..........[INSTALLATION] This configuration will allow Certbot to verify domain ownership." >&2

    # Start nginx with HTTP-only configuration
    cp nginx-http.conf nginx.conf
    echo "..........[INSTALLATION] Starting Nginx container with temporary configuration..." >&2
    docker compose up -d nginx  

    # Wait for nginx to start
    wait_for_container "nginx"

    echo "..........[INSTALLATION] Temporary Nginx setup complete." >&2
    echo "..........[INSTALLATION] Nginx is now ready to respond to the ACME challenge for SSL certificate issuance." >&2
}

configure_ssl() {
    echo "..........[INSTALLATION] Configuring SSL with Certbot..." >&2

    # Run certbot
    docker compose run --rm certbot certonly --webroot -w /var/www/certbot -d $domain_name --email $email_address --agree-tos --no-eff-email
}

replace_nginx_config_with_production_one(){  
    # Prepare HTTPS nginx configuration
    cat > nginx-https.conf <<EOF
events {
    worker_connections 1024;
}

http {
    # General settings
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;
  
    # File upload settings
    client_max_body_size 16M;
    client_body_buffer_size 128k;
    client_body_timeout 60s;

    # File download optimizations
    output_buffers 1 512k;
    postpone_output 1460;
    aio threads;
  
    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_buffers 16 8k;
    gzip_http_version 1.1;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml application/xml+rss text/javascript;
  
    # Cache settings
    open_file_cache max=1000 inactive=20s;
    open_file_cache_valid 30s;
    open_file_cache_min_uses 2;
    open_file_cache_errors on;
    
    server {
        listen 80;
        server_name $domain_name;

        location /.well-known/acme-challenge/ {
            root /var/www/certbot;
        }

        location / {
            return 301 https://\$host\$request_uri;
        }
    }

    server {
        listen 443 ssl;
        http2 on;
        server_name $domain_name;

        ssl_certificate /etc/nginx/ssl/live/$domain_name/fullchain.pem;
        ssl_certificate_key /etc/nginx/ssl/live/$domain_name/privkey.pem;

        # Improve SSL settings
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_prefer_server_ciphers on;
        ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:DHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384;
        ssl_session_cache shared:SSL:10m;
        ssl_session_timeout 10m;
        ssl_session_tickets off;

        # HSTS (optional, but recommended)
        add_header Strict-Transport-Security "max-age=31536000" always;

        location / {
            proxy_pass http://plikshare:8080;
            proxy_set_header Host \$host;
            proxy_set_header X-Real-IP \$remote_addr;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;

            # HTTP/2 specific headers
            proxy_set_header Upgrade \$http_upgrade;
            proxy_set_header Connection "upgrade";

            # Adjust proxy timeouts for 15MB chunked uploads
            proxy_connect_timeout 60s;
            proxy_send_timeout 60s;
            proxy_read_timeout 60s;

            # Enable request buffering
            proxy_request_buffering on;

            # Optimize for chunked uploads
            proxy_http_version 1.1;
                        
            # Large file optimizations
            proxy_max_temp_file_size 0;
            proxy_temp_file_write_size 64k;
        }
    }
}
EOF

    # Replace nginx configuration, start plikshare, and restart nginx
    cp nginx-https.conf nginx.conf
    docker compose up -d plikshare
    docker compose up -d --force-recreate nginx

    # Clean up temporary files
    rm nginx-http.conf nginx-https.conf
    
    
    echo "
===================================
  PlikShare Installation Complete
===================================

Your PlikShare service is now configured with HTTPS support.
- PlikShare is running and accessible via HTTPS
- HTTP requests will automatically redirect to HTTPS
- Nginx is optimized for file uploads and downloads
- Enhanced security settings are in place

You can now access your PlikShare instance securely at: https://$domain_name
" >&2
}

install_plikshare() {
    echo "
===============================
  3. PlikShare - Installation
===============================

We are now ready to install PlikShare on your server. This process involves several steps:

1. Generating a Docker Compose file tailored to your configuration.
2. Setting up a basic Nginx configuration for SSL certificate acquisition.
3. Configuring SSL using Let's Encrypt and Certbot.
4. Replacing the Nginx configuration with a production-ready setup.

This installation will set up PlikShare with HTTPS support, ensuring secure access to your instance.

NOTE: This process may take several minutes to complete, depending on your server's performance and network speed.
" >&2

    if ! ask_yes_no "Do you want to proceed with the PlikShare installation?" "..........[INSTALLATION] Proceeding with installation..." "..........[INSTALLATION] Installation aborted by user."; then
        return 1
    fi

    if ! generate_docker_compose; then
        echo "[ERROR] Installation aborted due to Docker Compose file generation failure." >&2
        return 1
    fi

    if ! setup_basic_nginx_for_ssl_setup; then
        echo "[ERROR] Installation aborted due to Nginx setup failure." >&2
        return 1
    fi

    if ! configure_ssl; then
        echo "[ERROR] Installation aborted due to SSL configuration failure." >&2
        return 1
    fi
    
    if ! replace_nginx_config_with_production_one; then
        echo "[ERROR] Installation aborted due to Nginx final configuration failure." >&2
        return 1
    fi

    return 0
}

setup_cron_jobs() {
    echo "
===================================
  4. PlikShare - Cron Jobs Setup
===================================

We'll now set up automatic SSL certificate renewal and optionally schedule nightly updates for PlikShare.
" >&2

    # Function to remove existing cron jobs
    remove_existing_cron_jobs() {
        echo "..........[CRON] Removing existing PlikShare-related cron jobs..." >&2
        crontab -l | grep -v "certbot renew" | grep -v "update_plikshare.sh" | crontab -
    }

    # Function to add certbot renewal cron job
    add_certbot_cron_job() {
        echo "..........[CRON] Setting up SSL certificate auto-renewal cron job..." >&2
        (crontab -l 2>/dev/null; echo "0 12 * * * docker compose -f $(pwd)/docker-compose.yml run --rm certbot renew --webroot -w /var/www/certbot --quiet && docker compose -f $(pwd)/docker-compose.yml exec nginx nginx -s reload") | crontab -
        echo "..........[CRON] Cron job for SSL certificate auto-renewal has been set up." >&2
    }

    # Function to add Plikshare update cron job
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

    # Remove existing cron jobs
    remove_existing_cron_jobs

    # Set up auto-renewal cron job
    add_certbot_cron_job

    echo ""

    # Ask user if they want to schedule nightly updates
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
===============================

Welcome to the PlikShare installation process. 
This script will guide you through setting up PlikShare on your server.
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