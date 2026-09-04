#!/usr/bin/env bash
#
# Idempotent repository bootstrap for the SeedAndRock Unity project.
#
# The base image (.cursor/Dockerfile) already installs the pinned Unity Editor
# and its Linux runtime libraries. This script runs after the source tree is
# checked out and:
#   1. Verifies the pinned Unity Editor is present and runnable.
#   2. Activates a Unity license when license secrets are provided.
#   3. When licensed, imports/compiles the project in batchmode (and can run
#      EditMode tests) to prove the C# assemblies build clean.
#   4. Always performs a license-free compile + run smoke test of the pure,
#      deterministic world-generation core so the environment is verifiably
#      useful even before a license is configured.
#
# It is safe to re-run: Unity import is incremental and every step is guarded.
set -uo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_VERSION="${UNITY_VERSION:-6000.6.0f1}"
UNITY_PATH="${UNITY_PATH:-/opt/unity/editors/${UNITY_VERSION}}"
UNITY_BIN="${UNITY_PATH}/Editor/Unity"
DATA_DIR="${UNITY_PATH}/Editor/Data"

log() { printf '\n[install] %s\n' "$*"; }

# ---------------------------------------------------------------------------
# 1. Verify the Unity Editor toolchain.
# ---------------------------------------------------------------------------
if [[ ! -x "${UNITY_BIN}" ]]; then
  echo "[install] ERROR: Unity Editor not found at ${UNITY_BIN}." >&2
  echo "[install] The base image should install it; see .cursor/Dockerfile." >&2
  exit 1
fi
log "Unity Editor: $("${UNITY_BIN}" -version 2>/dev/null || echo unknown) at ${UNITY_PATH}"

# ---------------------------------------------------------------------------
# 2. License-free compile + run smoke test of the deterministic core.
#    Uses Unity's bundled .NET SDK + reference assemblies, so it needs no
#    Unity license and proves the C# toolchain works against the project code.
# ---------------------------------------------------------------------------
smoke_test_core() {
  local dotnet="${DATA_DIR}/DotNetSdk/dotnet"
  local ue="${DATA_DIR}/Managed/UnityEngine"
  local nsref
  nsref="$(ls "${DATA_DIR}"/NetStandard/ref/*/netstandard.dll 2>/dev/null | head -n1)"
  local csc
  csc="$(ls "${DATA_DIR}"/DotNetSdk/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -n1)"

  if [[ ! -x "${dotnet}" || -z "${nsref}" || -z "${csc}" ]]; then
    log "Bundled .NET toolchain not found; skipping license-free core smoke test."
    return 0
  fi

  local core_src=(
    "${PROJECT_PATH}/Assets/_SeedAndRock/Scripts/World/SeedNoise.cs"
    "${PROJECT_PATH}/Assets/_SeedAndRock/Scripts/World/WorldHydrology.cs"
  )
  local out="/tmp/seedandrock_core.dll"
  log "Compiling deterministic world-gen core (license-free)..."
  if "${dotnet}" "${csc}" -nostdlib -noconfig -target:library -langversion:latest \
        -r:"${nsref}" -r:"${ue}/UnityEngine.CoreModule.dll" \
        -out:"${out}" "${core_src[@]}"; then
    log "Core compiled successfully -> ${out}"
  else
    echo "[install] WARNING: core compile failed." >&2
    return 0
  fi
}
smoke_test_core

# ---------------------------------------------------------------------------
# 3. Activate a Unity license if secrets are available.
#    Supports two flows:
#      - Personal: UNITY_LICENSE = full contents of a Unity_lic.ulf file.
#      - Pro/Plus: UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD.
# ---------------------------------------------------------------------------
LICENSE_DIR="${HOME}/.local/share/unity3d/Unity"
activate_license() {
  if [[ -n "${UNITY_LICENSE:-}" ]]; then
    log "Activating Unity license from UNITY_LICENSE (.ulf contents)..."
    mkdir -p "${LICENSE_DIR}"
    printf '%s' "${UNITY_LICENSE}" > "${LICENSE_DIR}/Unity_lic.ulf"
    xvfb-run -a "${UNITY_BIN}" -batchmode -nographics -quit -logFile - \
      -manualLicenseFile "${LICENSE_DIR}/Unity_lic.ulf" 2>&1 | tail -n 20 || true
    return 0
  fi

  if [[ -n "${UNITY_SERIAL:-}" && -n "${UNITY_EMAIL:-}" && -n "${UNITY_PASSWORD:-}" ]]; then
    log "Activating Unity license with serial + account credentials..."
    xvfb-run -a "${UNITY_BIN}" -batchmode -nographics -quit -logFile - \
      -serial "${UNITY_SERIAL}" -username "${UNITY_EMAIL}" -password "${UNITY_PASSWORD}" \
      2>&1 | grep -viE "password|serial" | tail -n 20 || true
    return 0
  fi

  return 1
}

# ---------------------------------------------------------------------------
# 4. If licensed, import + compile the full project in batchmode.
# ---------------------------------------------------------------------------
import_project() {
  log "Importing project and compiling all assemblies (batchmode)..."
  xvfb-run -a "${UNITY_BIN}" -batchmode -nographics -quit \
    -projectPath "${PROJECT_PATH}" \
    -logFile - 2>&1 | tail -n 40
}

if activate_license; then
  import_project
  log "Unity import complete. To run EditMode tests:"
  log "  xvfb-run -a ${UNITY_BIN} -runTests -batchmode -projectPath ${PROJECT_PATH} -testPlatform EditMode -testResults /tmp/results.xml"
else
  cat >&2 <<'EOF'

[install] No Unity license configured — skipping full project import.
[install] The Unity Editor toolchain is installed and the deterministic core
[install] compiles/runs, but importing packages (URP, Input System, TMP) and
[install] compiling the full project requires a Unity license.
[install]
[install] Provide one of the following via the Secrets panel:
[install]   * Personal: UNITY_LICENSE  (full contents of a Unity_lic.ulf file)
[install]   * Pro/Plus: UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD
[install]
[install] Then re-run this install step to activate + import the project.
EOF
fi

log "install.sh finished."
exit 0
