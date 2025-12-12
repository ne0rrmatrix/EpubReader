#!/usr/bin/env bash
set -e

GJSON="${1:-./build-secrets/google-services.json}"
OUT="${2:-./build-secrets/android/strings.secrets.xml}"

if [[ ! -f "$GJSON" ]]; then
  echo "google-services.json not found at $GJSON"
  exit 1
fi

mkdir -p "$(dirname "$OUT")"

API_KEY=$(jq -r '.client[0].api_key[0].current_key' "$GJSON")
APP_ID=$(jq -r '.client[0].client_info.mobilesdk_app_id' "$GJSON")
WEB_CLIENT_ID=$(jq -r '.client[0].oauth_client[] | select(.client_type==3) | .client_id' "$GJSON" | head -n 1)
FIREBASE_URL=$(jq -r '.project_info.firebase_url' "$GJSON")

cat > "$OUT" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="google_api_key">${API_KEY}</string>
    <string name="firebase_auth_domain">${FIREBASE_URL}</string>
    <string name="firebase_database_url">${FIREBASE_URL}</string>
    <string name="google_app_id">${APP_ID}</string>
    <string name="default_web_client_id">${WEB_CLIENT_ID}</string>
</resources>
EOF

echo "Generated $OUT"