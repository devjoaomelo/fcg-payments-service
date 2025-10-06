# -------- build stage --------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia os csproj para restaurar dependências
COPY ./src/FCG.Payments.Api/FCG.Payments.Api.csproj FCG.Payments.Api/
COPY ./src/FCG.Payments.Application/FCG.Payments.Application.csproj FCG.Payments.Application/
COPY ./src/FCG.Payments.Domain/FCG.Payments.Domain.csproj FCG.Payments.Domain/
COPY ./src/FCG.Payments.Infra/FCG.Payments.Infra.csproj FCG.Payments.Infra/
RUN dotnet restore FCG.Payments.Api/FCG.Payments.Api.csproj

# Copia o restante do código
COPY ./src/ ./

# Publica a aplicação
RUN dotnet publish FCG.Payments.Api/FCG.Payments.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# -------- runtime stage --------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FCG.Payments.Api.dll"]
