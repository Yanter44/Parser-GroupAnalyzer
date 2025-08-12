FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 9090

RUN apt-get update && \
    apt-get install -y \
      dnsutils \       
      iputils-ping \  
      curl \            
      openssl \       
      ca-certificates && \
    rm -rf /var/lib/apt/lists/*

RUN mkdir -p /persistent_storage/sessions && \
    ln -s /persistent_storage/sessions /app/sessions

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

ENV NUGET_REQUEST_TIMEOUT=600

COPY ["MpParserAPI.csproj", "."]
RUN dotnet restore "MpParserAPI.csproj"
COPY . .
RUN dotnet build "MpParserAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build


FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MpParserAPI.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false


FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MpParserAPI.dll"]