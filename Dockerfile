# ============================================
# STAGE 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copiar .csproj e restaurar dependências
COPY src/FCG.Payments.Api/FCG.Payments.Api.csproj FCG.Payments.Api/
COPY src/FCG.Payments.Application/FCG.Payments.Application.csproj FCG.Payments.Application/
COPY src/FCG.Payments.Domain/FCG.Payments.Domain.csproj FCG.Payments.Domain/
COPY src/FCG.Payments.Infra/FCG.Payments.Infra.csproj FCG.Payments.Infra/
RUN dotnet restore FCG.Payments.Api/FCG.Payments.Api.csproj

# Copiar código e publicar
COPY src/ ./
RUN dotnet publish FCG.Payments.Api/FCG.Payments.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ============================================
# STAGE 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final

# Instalar curl para healthcheck
RUN apk add --no-cache curl

WORKDIR /app
EXPOSE 8080

# Usuário não-root (segurança)
RUN adduser -u 1000 --disabled-password --gecos "" appuser && \
    chown -R appuser:appuser /app
USER appuser

# Copiar arquivos publicados
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FCG.Payments.Api.dll"]
