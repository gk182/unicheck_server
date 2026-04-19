# 🚀 HƯỚNG DẪN SETUP SERVER DIGITALOCEAN TỪNG BƯỚC

## ✅ BƯỚC 1: SSH VÀO SERVER

### 1.1 Lấy IP Server
- Vào [DigitalOcean Dashboard](https://cloud.digitalocean.com)
- Chọn **Droplet** → Lấy **IPv4 Address** (ví dụ: `123.45.67.89`)

### 1.2 SSH vào server (từ máy tính của bạn)

**Windows (PowerShell):**
```powershell
ssh root@123.45.67.89
# Nhập password root (được gửi qua email từ DigitalOcean)
```

**Mac/Linux:**
```bash
ssh root@123.45.67.89
```

✅ Bạn đã vào server!

---

## ✅ BƯỚC 2: CẬP NHẬT HỆ THỐNG

```bash
# Cập nhật package manager
apt update && apt upgrade -y

# Cài các package cần thiết
apt install -y curl wget git
```

⏳ Chờ khoảng 2-5 phút tùy tốc độ server.

---

## ✅ BƯỚC 3: CÀI ĐẶT DOCKER

### 3.1 Thêm Docker Repository

```bash
# Cài dependencies
apt install -y apt-transport-https ca-certificates curl gnupg lsb-release

# Thêm Docker GPG key
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg

# Thêm Docker repository
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null

# Update lại package list
apt update
```

### 3.2 Cài Docker Engine

```bash
apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Kiểm tra Docker đã cài
docker --version
docker compose version
```

**Output kỳ vọng:**
```
Docker version 27.x.x
Docker Compose version vX.X.X
```

### 3.3 Cấu hình Docker (chạy mà không cần sudo)

```bash
# Thêm user root vào Docker group
usermod -aG docker root

# Kiểm tra (nếu là root thì bỏ qua)
newgrp docker
```

---

## ✅ BƯỚC 4: CÀI ĐẶT GIT & CẤU HÌNH SSH KEY

### 4.1 Cài Git

```bash
apt install -y git
git --version
```

### 4.2 Tạo SSH Key cho GitHub

```bash
# Tạo SSH key
ssh-keygen -t rsa -b 4096 -f ~/.ssh/id_rsa -N ""

# Hiển thị public key
cat ~/.ssh/id_rsa.pub
```

**Copy toàn bộ output** (từ `ssh-rsa` đến cuối)

### 4.3 Thêm SSH Key vào GitHub

1. Vào [GitHub → Settings → SSH and GPG keys](https://github.com/settings/keys)
2. Click **New SSH key**
3. Title: `DigitalOcean Server`
4. Paste nội dung từ `cat ~/.ssh/id_rsa.pub`
5. Click **Add SSH key**

### 4.4 Kiểm tra kết nối GitHub

```bash
ssh -T git@github.com
# Output: "Hi USERNAME! You've successfully authenticated..."
```

✅ SSH key đã setup!

---

## ✅ BƯỚC 5: CLONE REPOSITORY

```bash
cd /root

# Clone repo (thay YOUR_USERNAME bằng username GitHub của bạn)
git clone git@github.com:YOUR_USERNAME/unicheck_server.git

# Vào thư mục project
cd unicheck_server

# Kiểm tra
ls -la
```

**Output kỳ vọng:**
```
drwxr-xr-x  docker-compose.yml
drwxr-xr-x  unicheck_backend/
drwxr-xr-x  unicheck_ai/
drwxr-xr-x  .github/
```

---

## ✅ BƯỚC 6: TẠO .ENV FILE

### 6.1 Tạo file .env từ .env.example

```bash
# Copy .env.example thành .env
cp .env.example .env

# Xem nội dung
cat .env
```

### 6.2 Chỉnh sửa .env (nếu cần password khác)

```bash
# Mở file .env bằng nano editor
nano .env
```

**Nội dung cần sửa:**
```env
# SQL Server Configuration
SQL_SA_PASSWORD=UniCheck@Secure2025!@#   # ← Thay password tại đây
SQL_ACCEPT_EULA=Y

# .NET Backend Configuration
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
DB_CONNECTION_STRING=Server=unicheck_sql,1433;Database=UniCheckDB;User Id=sa;Password=UniCheck@Secure2025!@#;Encrypt=False;TrustServerCertificate=True;

# Python AI Configuration
PYTHONUNBUFFERED=1
API_HOST=0.0.0.0
API_PORT=8000
```

**Lưu file:** Nhấn `Ctrl + O` → `Enter` → `Ctrl + X`

### 6.3 Kiểm tra file .env

```bash
cat .env
```

✅ File .env đã tạo!

---

## ✅ BƯỚC 7: KHỞI ĐỘNG DOCKER COMPOSE

### 7.1 Kiểm tra docker-compose.yml

```bash
# Đảm bảo bạn đang trong /root/unicheck_server
pwd

# Kiểm tra cấu hình
docker compose config
```

**Output kỳ vọng:** Hiển thị toàn bộ cấu hình services

### 7.2 Build & Run Docker

```bash
# Lần đầu (build image & run)
docker compose up -d --build

# Chờ khoảng 3-5 phút (CPU sẽ cao)
```

**Quá trình build:**
```
[+] Running 5/5
 ✔ Network unicheck_net Created
 ✔ Volume sql_data Created
 ✔ unicheck_sql Started (pulling image...)
 ✔ unicheck_dotnet Started (building...)
 ✔ unicheck_python Started (building...)
```

---

## ✅ BƯỚC 8: KIỂM TRA CONTAINERS

```bash
# Xem status containers
docker compose ps

# Output kỳ vọng:
# NAME                 STATUS           PORTS
# unicheck_sql         Up 2 minutes      0.0.0.0:1433->1433/tcp
# unicheck_dotnet      Up 1 minute       0.0.0.0:8080->8080/tcp
# unicheck_python      Up 1 minute       0.0.0.0:8000->8000/tcp
```

### Nếu có container chưa Up:

```bash
# Xem logs của service
docker compose logs unicheck_sql
docker compose logs unicheck_dotnet
docker compose logs unicheck_python

# Xem log real-time (-f = follow)
docker compose logs -f unicheck_dotnet
```

---

## ✅ BƯỚC 9: KIỂM TRA API HOẠT ĐỘNG

### 9.1 Kiểm tra SQL Server

```bash
# Qua Docker
docker compose exec unicheck_sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YOUR_PASSWORD" -Q "SELECT 1"

# Hoặc từ bên ngoài
curl -f http://123.45.67.89:1433 && echo "SQL Server Running" || echo "SQL Server Not Responding"
```

### 9.2 Kiểm tra .NET API

```bash
# Qua Docker
docker compose exec unicheck_dotnet curl http://localhost:8080/health

# Hoặc từ bên ngoài
curl http://123.45.67.89:8080/api/endpoint
```

### 9.3 Kiểm tra Python AI

```bash
# Qua Docker
docker compose exec unicheck_python curl http://localhost:8000/health

# Hoặc từ bên ngoài
curl http://123.45.67.89:8000/health
```

---

## ✅ BƯỚC 10: CẤU HÌNH FIREWALL (TÙY CHỌN)

Nếu muốn chỉ cho phép các port cần thiết:

```bash
# Cài UFW
apt install -y ufw

# Cho phép SSH (rất quan trọng!)
ufw allow 22/tcp

# Cho phép Docker ports
ufw allow 1433/tcp   # SQL Server
ufw allow 8080/tcp   # .NET API
ufw allow 8000/tcp   # Python AI

# Bật firewall
ufw enable

# Kiểm tra
ufw status
```

---

## ✅ BƯỚC 11: CẤU HÌNH AUTO-START (SAU KHI REBOOT)

```bash
# Cho Docker daemon tự start
sudo systemctl enable docker
sudo systemctl enable containerd.d

# Kiểm tra
systemctl is-enabled docker
```

---

## ✅ BƯỚC 12: SETUP GITHUB SECRETS (Để CI/CD hoạt động)

### 12.1 Lấy SSH Private Key

```bash
# Từ server, hiển thị private key
cat ~/.ssh/id_rsa
```

**Copy toàn bộ output** (từ `-----BEGIN` đến `-----END`)

### 12.2 Cấu hình GitHub Secrets

1. Vào repo GitHub → **Settings** → **Secrets and variables** → **Actions**
2. Click **New repository secret**
3. Thêm 3 secrets:

| Secret Name | Giá trị |
|------------|--------|
| `HOST` | IP server, ví dụ: `123.45.67.89` |
| `USERNAME` | `root` |
| `SSH_KEY` | Nội dung từ `cat ~/.ssh/id_rsa` |

---

## 📊 TỔNG KẾT - LỆNH QUAN TRỌNG

```bash
# Khởi động services
docker compose up -d --build

# Dừng services
docker compose down

# Xem logs
docker compose logs -f

# Restart services
docker compose restart

# Xóa volumes (CẢNH BÁO: Mất dữ liệu!)
docker compose down -v

# Dọn rác Docker
docker system prune -a --volumes
```

---

## 🆘 TROUBLESHOOTING

### Lỗi: "docker: command not found"
```bash
# Docker chưa được cài
apt install -y docker.io
```

### Lỗi: "Cannot connect to Docker daemon"
```bash
# Khởi động Docker
systemctl start docker
```

### Lỗi: "Port 1433 already in use"
```bash
# Xem process sử dụng port
lsof -i :1433

# Hoặc xóa volume cũ
docker compose down -v
docker compose up -d --build
```

### Lỗi: "Connection refused" khi test API
```bash
# Chờ thêm thời gian để services khởi động
sleep 30
docker compose ps

# Nếu vẫn không on, xem logs
docker compose logs unicheck_dotnet
```

### Lỗi: "Exit code 137" (Out of Memory)
```bash
# Server không đủ RAM
# Xóa services khác hoặc upgrade droplet
docker system prune -a --volumes
```

---

## ✅ KIỂM TRA HOÀN THÀNH

Khi tất cả xong, bạn sẽ có:

- ✅ Docker cài đặt
- ✅ Git clone code từ GitHub
- ✅ SQL Server chạy trên port 1433
- ✅ .NET API chạy trên port 8080
- ✅ Python AI chạy trên port 8000
- ✅ SSH keys setup cho GitHub Auto Deployment
- ✅ GitHub Actions CI/CD sẵn sàng

### Test Deploy Tự Động

```bash
# Từ máy tính của bạn
cd unicheck_server
git commit -m "test deployment" --allow-empty
git push origin main

# Theo dõi deployment trên GitHub Actions
# GitHub → Actions → Deploy UniCheck Server
```

---

**Nếu bạn gặp vấn đề ở bước nào, hãy copy-paste error message để tôi hỗ trợ!** 🚀
