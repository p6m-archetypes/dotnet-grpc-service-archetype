# local/Dockerfile — for kind cluster local development
# Load with: kind load docker-image <image>:<tag>

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder
WORKDIR /src

# protoc is required by Grpc.Tools at build time. Use the distro protoc, not the one bundled in
# the Grpc.Tools nuget: the bundled linux_arm64 binary segfaults when spawned by MSBuild
# (grpc/grpc#26032 class of bug), which breaks arm64 image builds. MSBuild picks the env var up
# as the Protobuf_ProtocFullPath property.
RUN apt-get update && apt-get install -y --no-install-recommends protobuf-compiler && \
    rm -rf /var/lib/apt/lists/*
ENV Protobuf_ProtocFullPath=/usr/bin/protoc

COPY . .
RUN dotnet restore "{{ PrefixName }}{{ SuffixName }}/{{ PrefixName }}{{ SuffixName }}.csproj" && \
    dotnet publish "{{ PrefixName }}{{ SuffixName }}/{{ PrefixName }}{{ SuffixName }}.csproj" \
        -c Release \
        -o /app/publish \
        --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Run as non-root user (built-in 'app' user in aspnet image, uid 1654)
USER app

COPY --from=builder --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=""
EXPOSE {{ service_port }}
EXPOSE {{ management_port }}

ENTRYPOINT ["dotnet", "{{ PrefixName }}{{ SuffixName }}.dll"]
