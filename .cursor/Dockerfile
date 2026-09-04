# Cloud Agent base image for the SeedAndRock Unity 6 (URP) project.
#
# Installs the exact Unity Editor the project is pinned to (see
# ProjectSettings/ProjectVersion.txt) plus the Linux runtime libraries the
# headless editor needs. Repository-dependent work (license activation and the
# project import/compile) happens later in .cursor/install.sh, once the source
# tree and Unity license secrets are available.
FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive

# Pinned Unity Editor. Keep in sync with ProjectSettings/ProjectVersion.txt.
ARG UNITY_VERSION=6000.6.0f1
ARG UNITY_REVISION=f7f8ed4d1e24

# Base tooling + Unity Editor Linux runtime dependencies. The library set mirrors
# what the headless editor loads (GTK/NSS/GL/ALSA/X11/CUPS/fontconfig, etc.).
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl git git-lfs xz-utils sudo locales \
        xvfb xauth x11-utils \
        libgtk-3-0 libnss3 libnotify4 libglu1-mesa \
        libasound2t64 libxtst6 libxss1 libgbm1 libgl1 libglx-mesa0 libgl1-mesa-dri \
        libatk1.0-0t64 libatk-bridge2.0-0t64 libgdk-pixbuf-2.0-0 \
        libx11-xcb1 libxcursor1 libxrandr2 libxi6 libxcomposite1 libxdamage1 \
        libcups2t64 fontconfig libfontconfig1 libunwind8 libssl3 \
        libncurses6 libtcmalloc-minimal4 desktop-file-utils \
    && rm -rf /var/lib/apt/lists/*

# Download and unpack the pinned Unity Editor into a stable location.
RUN mkdir -p "/opt/unity/editors/${UNITY_VERSION}" \
    && curl -fL --retry 5 --retry-delay 4 -o /tmp/Unity.tar.xz \
        "https://download.unity3d.com/download_unity/${UNITY_REVISION}/LinuxEditorInstaller/Unity.tar.xz" \
    && tar -xf /tmp/Unity.tar.xz -C "/opt/unity/editors/${UNITY_VERSION}" \
    && rm -f /tmp/Unity.tar.xz \
    && ln -sf "/opt/unity/editors/${UNITY_VERSION}/Editor/Unity" /usr/local/bin/unity-editor

ENV UNITY_VERSION=${UNITY_VERSION}
ENV UNITY_PATH=/opt/unity/editors/${UNITY_VERSION}

# The Cloud Agent runs as the pre-existing `ubuntu` user shipped in the noble image.
USER ubuntu
