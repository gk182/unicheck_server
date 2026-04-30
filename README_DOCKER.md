# UniCheck Docker Setup Guide

This file explains how to run the UniCheck system with Docker step by step.

## What runs in Docker

The project is started by `docker-compose.yml` with 3 services:

- `unicheck_sql`: SQL Server database
- `unicheck_dotnet`: ASP.NET Core backend
- `unicheck_python`: Python AI service for face recognition

The startup order is:

1. SQL Server starts first.
2. Backend waits until SQL Server is healthy.
3. Python AI service starts in the same Docker network.
4. Backend applies migrations and seeds data because `RUN_DB_MIGRATION=true` and `RUN_DB_SEEDER=true` are set in Compose.

## Ports

- SQL Server: `1433`
- Backend: `8080`
- Python AI: `8000`

## Prerequisites

Make sure you already have:

- Docker Desktop installed
- Docker Compose available
- This repository cloned on your machine

## Step 1: Create the `.env` file

The Compose file uses variables from `.env`.

1. Copy `.env.example` to `.env`.
2. Update the values if needed.

Example:

```env
SQL_SA_PASSWORD=Admin@123
SQL_ACCEPT_EULA=Y
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
DB_CONNECTION_STRING=Server=unicheck_sql,1433;Database=UniCheckDB;User Id=sa;Password=Admin@123;Encrypt=False;TrustServerCertificate=True;
PYTHONUNBUFFERED=1
API_HOST=0.0.0.0
API_PORT=8000
```

Important notes:

- `DB_CONNECTION_STRING` should use `unicheck_sql`, not `localhost`, when running inside Docker.
- `ASPNETCORE_ENVIRONMENT=Production` makes the backend load `appsettings.Production.json`.
- The environment variable `ConnectionStrings__DefaultConnection` overrides the connection string in JSON config.

## Step 2: Build and start the containers

From the repository root, run:

```bash
docker compose up -d --build
```

What this does:

- builds the backend image from `unicheck_backend/Dockerfile`
- builds the Python image from `unicheck_ai/Dockerfile`
- starts SQL Server first
- starts the backend after SQL is healthy
- starts the Python AI service

## Step 3: Check container status

```bash
docker compose ps
```

You should see all services in the `Up` state.

## Step 4: Check logs if something fails

If a service does not start correctly, inspect its logs:

```bash
docker compose logs unicheck_sql
docker compose logs unicheck_dotnet
docker compose logs unicheck_python
```

For live logs:

```bash
docker compose logs -f unicheck_dotnet
```

## Step 5: Open the application

After the containers are running:

- Backend: `http://localhost:8080`
- Python AI service: `http://localhost:8000`

The backend will call the Python service using the Docker service name `unicheck_python` inside the shared network.

## Step 6: Stop the system

To stop all containers:

```bash
docker compose down
```

To stop and remove volumes too:

```bash
docker compose down -v
```

Use `down -v` only if you want to delete the SQL data volume.

## How config is loaded

Inside the backend container, ASP.NET Core reads configuration in this order:

1. `appsettings.json`
2. `appsettings.Production.json` or `appsettings.Development.json`, depending on `ASPNETCORE_ENVIRONMENT`
3. environment variables from Docker Compose

That means Docker values can override the JSON files.

## Common problems

### Backend cannot connect to SQL Server

Check these points:

- `DB_CONNECTION_STRING` uses `unicheck_sql,1433`
- SQL Server container is healthy
- the password in `.env` matches `SQL_SA_PASSWORD`

### Python service cannot be reached

Check these points:

- `unicheck_python` container is running
- `FaceAI:BaseUrl` points to `http://unicheck_python:8000` in production

### Backend starts but data is missing

The backend runs migration and seeding on startup because these values are set in Compose:

- `RUN_DB_MIGRATION=true`
- `RUN_DB_SEEDER=true`

If you do not want that behavior, change them in `docker-compose.yml`.

## Quick summary

If you only want the shortest run flow:

```bash
cp .env.example .env
docker compose up -d --build
docker compose ps
docker compose logs -f unicheck_dotnet
```
