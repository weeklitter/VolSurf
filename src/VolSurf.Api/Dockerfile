# ── build stage ──
# 镜像源：m.daocloud.io/mcr.microsoft.com（国内代理，直连 mcr 不稳）
FROM m.daocloud.io/mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制 csproj 并 restore（缓存依赖层）
COPY VolSurf.sln ./
COPY src/VolSurf.Api/VolSurf.Api.csproj   src/VolSurf.Api/
COPY src/VolSurf.Core/VolSurf.Core.csproj src/VolSurf.Core/
COPY src/VolSurf.Data/VolSurf.Data.csproj src/VolSurf.Data/
RUN dotnet restore src/VolSurf.Api/VolSurf.Api.csproj

# 复制源码并发布
COPY src/ src/
RUN dotnet publish src/VolSurf.Api/VolSurf.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── runtime stage ──
FROM m.daocloud.io/mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# 健康检查需要 curl
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

EXPOSE 8080

# Program.cs 启动时自动 Migrate（db.Database.Migrate()）
ENTRYPOINT ["dotnet", "VolSurf.Api.dll"]