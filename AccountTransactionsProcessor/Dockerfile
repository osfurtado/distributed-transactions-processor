# Unter https://aka.ms/customizecontainer erfahren Sie, wie Sie Ihren Debugcontainer anpassen und wie Visual Studio dieses Dockerfile verwendet, um Ihre Images für ein schnelleres Debuggen zu erstellen.

# Diese ARGs ermöglichen den Austausch der Basis, die zum Erstellen des endgültigen Images beim Debuggen von VS verwendet wird.
ARG LAUNCHING_FROM_VS
# Hiermit wird das Basisimage für "final" festgelegt, jedoch nur, wenn LAUNCHING_FROM_VS definiert wurde.
ARG FINAL_BASE_IMAGE=${LAUNCHING_FROM_VS:+aotdebug}

# Diese Stufe wird verwendet, wenn sie von VS im Schnellmodus ausgeführt wird (Standardeinstellung für Debugkonfiguration).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# Diese Stufe wird zum Erstellen des Dienstprojekts verwendet.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Installieren Sie clang/zlib1g-dev-Abhängigkeiten für die Veröffentlichung unter nativ
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    clang zlib1g-dev
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AccountTransactionsProcessor.csproj", "."]
RUN dotnet restore "./AccountTransactionsProcessor.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./AccountTransactionsProcessor.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Diese Stufe wird verwendet, um das Dienstprojekt zu veröffentlichen, das in die letzte Phase kopiert werden soll.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AccountTransactionsProcessor.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=true

# Diese Stufe wird als Basis für die letzte Stufe verwendet, wenn sie von VS gestartet wird, um das Debuggen im regulären Modus zu unterstützen (Standardwert, wenn die Debugkonfiguration nicht verwendet wird).
FROM base AS aotdebug
USER root
# Installieren Sie GDB, um natives Debuggen zu unterstützen.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    gdb
USER app

# Diese Stufe wird in der Produktion oder bei Ausführung von VS im regulären Modus verwendet (Standard, wenn die Debugkonfiguration nicht verwendet wird).
FROM ${FINAL_BASE_IMAGE:-mcr.microsoft.com/dotnet/runtime-deps:10.0} AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["./AccountTransactionsProcessor"]