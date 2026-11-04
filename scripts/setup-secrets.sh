#!/usr/bin/env bash
# Генерирует случайный ключ подписи JWT и сохраняет его в user-secrets.
# Ключ не попадает в git: user-secrets лежит вне репозитория.
set -euo pipefail

cd "$(dirname "$0")/../server/Afisha.Api"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Не найден dotnet. Установите .NET 10 SDK." >&2
  exit 1
fi

key="$(openssl rand -base64 48)"
dotnet user-secrets set "Jwt:Key" "$key" >/dev/null

echo "Jwt:Key сохранён в user-secrets для Afisha.Api."
