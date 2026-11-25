# Kotlin sandbox image: JDK 21 + kotlinc. Used by the Docker code-execution provider for "kotlin".
# The container is started with --network none, --read-only, a non-root user and dropped capabilities,
# so nothing here needs network access at runtime.
FROM eclipse-temurin:21-jdk

ARG KOTLIN_VERSION=2.0.21
# SHA-256 of kotlin-compiler-${KOTLIN_VERSION}.zip from the official GitHub release.
ARG KOTLIN_SHA256=""

RUN apt-get update \
 && apt-get install -y --no-install-recommends unzip curl ca-certificates \
 && curl -fsSL -o /tmp/kotlin.zip "https://github.com/JetBrains/kotlin/releases/download/v${KOTLIN_VERSION}/kotlin-compiler-${KOTLIN_VERSION}.zip" \
 && if [ -n "$KOTLIN_SHA256" ]; then echo "${KOTLIN_SHA256}  /tmp/kotlin.zip" | sha256sum -c -; fi \
 && unzip -q /tmp/kotlin.zip -d /opt \
 && rm /tmp/kotlin.zip \
 && apt-get purge -y curl unzip && apt-get autoremove -y && rm -rf /var/lib/apt/lists/*

ENV PATH="/opt/kotlinc/bin:${PATH}"
# Compilation happens inside the sandbox; keep the JVM small and deterministic.
ENV JAVA_TOOL_OPTIONS="-Xmx384m -XX:+UseSerialGC"
USER 65534:65534
WORKDIR /work
