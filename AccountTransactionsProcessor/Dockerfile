# Base ASP.NET (usado apenas para debug se necessário)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    clang zlib1g-dev

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["AccountTransactionsProcessor.csproj", "."]
RUN dotnet restore "./AccountTransactionsProcessor.csproj"

COPY . .
RUN dotnet build "./AccountTransactionsProcessor.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish (AOT)
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AccountTransactionsProcessor.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    -r linux-x64 \
    -p:PublishAot=true \
    -p:StripSymbols=true \
    -p:InvariantGlobalization=true

# Final (imagem mínima)
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final

WORKDIR /app
EXPOSE 8080

# Copia binário
COPY --from=publish /app/publish .

# Garante execução
RUN chmod +x /app/AccountTransactionsProcessor

# Cria usuário não-root
RUN useradd appuser
RUN chown -R appuser:appuser /app
USER appuser

# Ajusta permissões da pasta
RUN chown -R appuser:appuser /app

# Troca usuário
USER appuser

# Executa binário nativo (AOT)
ENTRYPOINT ["./AccountTransactionsProcessor"]