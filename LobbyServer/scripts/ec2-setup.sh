#!/bin/bash
set -e

echo "=== EC2 Docker setup (Amazon Linux 2023) ==="

sudo yum update -y
sudo yum install -y docker git
sudo systemctl enable docker
sudo systemctl start docker
sudo usermod -aG docker "$USER"

if ! docker compose version >/dev/null 2>&1; then
  sudo mkdir -p /usr/local/lib/docker/cli-plugins
  sudo curl -SL "https://github.com/docker/compose/releases/download/v2.29.2/docker-compose-linux-x86_64" \
    -o /usr/local/lib/docker/cli-plugins/docker-compose
  sudo chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
fi

echo ""
echo "[OK] Docker installed."
echo "     Log out and SSH back in, then run deploy commands."
docker --version
docker compose version
