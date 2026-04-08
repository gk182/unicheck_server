# ═══════════════════════════════════════════════════════════════
# PYTHON AI SERVICE - Face Recognition Dockerfile
# ═══════════════════════════════════════════════════════════════
FROM python:3.11-slim

WORKDIR /app

# Update package manager và install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    cmake \
    libopenblas-dev \
    liblapack-dev \
    libx11-6 \
    libxext6 \
    libxrender-dev \
    libgl1-mesa-glx \
    libglib2.0-0 \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements.txt
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY . .

# Expose FastAPI port
EXPOSE 8000

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

# Run FastAPI with Uvicorn (using main.py)
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
